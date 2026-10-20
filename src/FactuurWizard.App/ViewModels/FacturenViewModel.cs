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
    int Id, string Nummer, string Soort, DateOnly Datum, string Klant, decimal TotaalInclBtw, string Status, int Sortering);

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

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(OpenenCommand), nameof(PdfOpenenCommand))]
    public partial FactuurRij? Geselecteerd { get; set; }

    [ObservableProperty]
    public partial string Zoekterm { get; set; } = "";

    [ObservableProperty]
    public partial bool AlleenConcepten { get; set; }

    partial void OnZoektermChanged(string value) => FacturenView.Refresh();
    partial void OnAlleenConceptenChanged(bool value) => FacturenView.Refresh();

    private bool PastBijFilter(FactuurRij r) =>
        (!AlleenConcepten || r.Status == "Concept") &&
        (string.IsNullOrWhiteSpace(Zoekterm) ||
         r.Nummer.Contains(Zoekterm, StringComparison.OrdinalIgnoreCase) ||
         r.Klant.Contains(Zoekterm, StringComparison.OrdinalIgnoreCase));

    public override async Task LaadAsync()
    {
        var facturen = await Db.Facturen.AsNoTracking().Include(f => f.Klant).ToListAsync();
        Facturen.Clear();
        foreach (var f in facturen)
        {
            var concept = f.Status == FactuurStatus.Concept;
            Facturen.Add(new FactuurRij(
                f.Id,
                f.Factuurnummer ?? "",
                f.Soort == FactuurSoort.Creditfactuur ? "Creditfactuur" : "Factuur",
                f.Factuurdatum,
                concept ? f.Klant?.Naam ?? "" : f.KlantGegevens.Naam,
                f.TotaalInclBtw,
                concept ? "Concept" : "Definitief",
                concept ? int.MaxValue - f.Id : (f.Jaar ?? 0) * 100_000 + (f.Volgnummer ?? 0)));
        }
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

    private bool IsDefinitiefGeselecteerd() => Geselecteerd is { Status: "Definitief" };
}
