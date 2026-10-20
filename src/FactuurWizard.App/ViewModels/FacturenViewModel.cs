using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Windows.Data;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using FactuurWizard.App.Services;
using FactuurWizard.Core.Model;
using FactuurWizard.Data;
using Microsoft.EntityFrameworkCore;

namespace FactuurWizard.App.ViewModels;

public sealed record FactuurRij(
    int Id,
    string Nummer,
    string Soort,
    DateOnly Datum,
    DateOnly? Vervaldatum,
    string Klant,
    decimal TotaalInclBtw,
    decimal Openstaand,
    BetaalStatus Betaalstatus,
    bool IsConcept,
    int Herinneringen,
    int Sortering)
{
    public string Status => IsConcept ? "Concept" : Betaalstatus switch
    {
        BetaalStatus.Openstaand => "Openstaand",
        BetaalStatus.Vervallen => "Vervallen",
        BetaalStatus.Betaald => "Betaald",
        BetaalStatus.Gecrediteerd => "Gecrediteerd",
        _ => "Definitief",
    };

    public bool IsVervallen => Betaalstatus == BetaalStatus.Vervallen;
    public string OpenstaandTekst => Openstaand > 0 ? Openstaand.ToString("C") : "";
    public string HerinneringenTekst => Herinneringen == 0 ? "" : Herinneringen.ToString();
}

public enum StatusFilter
{
    Alle,
    Concepten,
    Openstaand,
    Vervallen,
    Betaald,
}

public partial class FacturenViewModel : PaginaViewModel
{
    private readonly Navigatie _navigatie;
    private readonly FactuurDocumenten _documenten;

    public FacturenViewModel(
        IDbContextFactory<FactuurWizardDbContext> dbFactory, Navigatie navigatie, FactuurDocumenten documenten)
        : base(dbFactory)
    {
        _navigatie = navigatie;
        _documenten = documenten;
        FacturenView = CollectionViewSource.GetDefaultView(Facturen);
        FacturenView.Filter = o => o is FactuurRij r && PastBijFilter(r);
        // Concepten bovenaan, daarna de nieuwste factuur eerst.
        FacturenView.SortDescriptions.Add(new SortDescription(nameof(FactuurRij.Sortering), ListSortDirection.Descending));
    }

    public override string Titel => "Facturen";

    public ObservableCollection<FactuurRij> Facturen { get; } = [];
    public ICollectionView FacturenView { get; }

    public IReadOnlyList<Keuze<StatusFilter>> StatusFilters { get; } =
    [
        new(StatusFilter.Alle, "Alle facturen"),
        new(StatusFilter.Concepten, "Concepten"),
        new(StatusFilter.Openstaand, "Openstaand (incl. vervallen)"),
        new(StatusFilter.Vervallen, "Vervallen"),
        new(StatusFilter.Betaald, "Betaald"),
    ];

    [ObservableProperty]
    public partial IReadOnlyList<Keuze<int>> Jaren { get; set; } = [];

    [ObservableProperty]
    public partial StatusFilter GekozenStatus { get; set; }

    /// <summary>0 = alle jaren.</summary>
    [ObservableProperty]
    public partial int GekozenJaar { get; set; }

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(OpenenCommand), nameof(PdfOpenenCommand))]
    public partial FactuurRij? Geselecteerd { get; set; }

    [ObservableProperty]
    public partial string Zoekterm { get; set; } = "";

    [ObservableProperty]
    public partial string Samenvatting { get; set; } = "";

    partial void OnZoektermChanged(string value) => FacturenView.Refresh();
    partial void OnGekozenStatusChanged(StatusFilter value) => FacturenView.Refresh();
    partial void OnGekozenJaarChanged(int value) => FacturenView.Refresh();

    private bool PastBijFilter(FactuurRij r)
    {
        var status = GekozenStatus switch
        {
            StatusFilter.Concepten => r.IsConcept,
            StatusFilter.Openstaand => r.Betaalstatus is BetaalStatus.Openstaand or BetaalStatus.Vervallen,
            StatusFilter.Vervallen => r.Betaalstatus == BetaalStatus.Vervallen,
            StatusFilter.Betaald => r.Betaalstatus == BetaalStatus.Betaald,
            _ => true,
        };
        if (!status || (GekozenJaar != 0 && r.Datum.Year != GekozenJaar))
            return false;
        if (string.IsNullOrWhiteSpace(Zoekterm))
            return true;

        var z = Zoekterm.Trim();
        return r.Nummer.Contains(z, StringComparison.OrdinalIgnoreCase) ||
               r.Klant.Contains(z, StringComparison.OrdinalIgnoreCase) ||
               r.TotaalInclBtw.ToString("0.00").Contains(z.Replace("€", "").Trim());
    }

    public override async Task LaadAsync()
    {
        var vandaag = DateOnly.FromDateTime(DateTime.Today);
        var facturen = await Db.Facturen.AsNoTracking().Include(f => f.Klant).Include(f => f.Herinneringen).ToListAsync();
        var gecrediteerd = facturen
            .Where(f => f.OrigineleFactuurId is not null && f.Status == FactuurStatus.Definitief)
            .GroupBy(f => f.OrigineleFactuurId!.Value)
            .ToDictionary(g => g.Key, g => g.Sum(f => f.TotaalInclBtw));

        Facturen.Clear();
        foreach (var f in facturen)
        {
            var concept = f.Status == FactuurStatus.Concept;
            var credit = gecrediteerd.GetValueOrDefault(f.Id);
            var status = f.BetaalStatusOp(vandaag, credit);
            Facturen.Add(new FactuurRij(
                f.Id,
                f.Factuurnummer ?? "",
                f.Soort == FactuurSoort.Creditfactuur ? "Creditfactuur" : "Factuur",
                f.Factuurdatum,
                f.Soort == FactuurSoort.Factuur ? f.Vervaldatum : null,
                concept ? f.Klant?.Naam ?? "" : f.KlantGegevens.Naam,
                f.TotaalInclBtw,
                status is BetaalStatus.Openstaand or BetaalStatus.Vervallen ? f.OpenstaandBedrag(credit) : 0m,
                status,
                concept,
                f.Herinneringen.Count,
                concept ? int.MaxValue - f.Id : (f.Jaar ?? 0) * 100_000 + (f.Volgnummer ?? 0)));
        }

        Jaren = [new Keuze<int>(0, "Alle jaren"), .. Facturen.Select(r => r.Datum.Year).Distinct().OrderDescending()
            .Select(j => new Keuze<int>(j, j.ToString()))];

        var open = Facturen.Where(r => r.Openstaand > 0).ToList();
        var vervallen = open.Where(r => r.IsVervallen).ToList();
        Samenvatting = open.Count == 0
            ? "Er staan geen facturen open."
            : $"Openstaand: {open.Sum(r => r.Openstaand):C} ({open.Count} {(open.Count == 1 ? "factuur" : "facturen")})" +
              (vervallen.Count > 0 ? $", waarvan vervallen: {vervallen.Sum(r => r.Openstaand):C} ({vervallen.Count})" : "");
    }

    [RelayCommand]
    private Task NieuweFactuurAsync() => _navigatie.OpenAsync<FactuurBewerkenViewModel>(p => p.Initialiseer(null));

    [RelayCommand(CanExecute = nameof(IsGeselecteerd))]
    private Task OpenenAsync() =>
        Geselecteerd is { } rij ? _navigatie.OpenAsync<FactuurBewerkenViewModel>(p => p.Initialiseer(rij.Id)) : Task.CompletedTask;

    private bool IsGeselecteerd() => Geselecteerd is not null;

    [RelayCommand(CanExecute = nameof(IsDefinitiefGeselecteerd))]
    private async Task PdfOpenenAsync()
    {
        if (Geselecteerd is not { } rij)
            return;
        try
        {
            var factuur = await Db.Facturen.Include(f => f.Regels).Include(f => f.Klant).SingleAsync(f => f.Id == rij.Id);
            FactuurDocumenten.Open(await _documenten.ZorgVoorPdfAsync(Db, factuur));
        }
        catch (Exception ex)
        {
            Foutlog.Schrijf(ex);
            ToonFouten([$"De PDF kon niet worden geopend: {ex.Message}"]);
        }
    }

    private bool IsDefinitiefGeselecteerd() => Geselecteerd is { IsConcept: false };
}
