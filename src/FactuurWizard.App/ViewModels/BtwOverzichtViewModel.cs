using System.IO;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using FactuurWizard.App.Services;
using FactuurWizard.Core.Model;
using FactuurWizard.Core.Services;
using FactuurWizard.Data;
using FactuurWizard.Export;
using Microsoft.EntityFrameworkCore;

namespace FactuurWizard.App.ViewModels;

public partial class BtwOverzichtViewModel(
    IDbContextFactory<FactuurWizardDbContext> dbFactory,
    Opslaglocatie opslag,
    DialoogService dialogen) : PaginaViewModel(dbFactory)
{
    private List<Factuur> _facturen = [];
    private Dictionary<int, BtwSoort> _groepSoorten = [];
    private string _bedrijfsnaam = "";
    private bool _geladen;

    public override string Titel => "Btw-overzicht";

    public IReadOnlyList<Keuze<int>> Kwartalen { get; } =
    [
        new(1, "1e kwartaal (jan–mrt)"),
        new(2, "2e kwartaal (apr–jun)"),
        new(3, "3e kwartaal (jul–sep)"),
        new(4, "4e kwartaal (okt–dec)"),
        new(0, "Heel jaar"),
    ];

    [ObservableProperty]
    public partial IReadOnlyList<int> Jaren { get; set; } = [];

    [ObservableProperty]
    public partial int GekozenJaar { get; set; }

    /// <summary>1–4, of 0 voor het hele jaar.</summary>
    [ObservableProperty]
    public partial int GekozenKwartaal { get; set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(KorTekst), nameof(AantalTekst))]
    [NotifyCanExecuteChangedFor(nameof(ExporteerCommand))]
    public partial BtwOverzicht? Overzicht { get; set; }

    partial void OnGekozenJaarChanged(int value) => Bereken();
    partial void OnGekozenKwartaalChanged(int value) => Bereken();

    public string PeriodeNaam => GekozenKwartaal == 0 ? $"{GekozenJaar}" : $"{GekozenJaar} kwartaal {GekozenKwartaal}";

    public string? KorTekst => Overzicht is { OmzetKor: not 0 } o
        ? $"Omzet onder de kleineondernemersregeling: {o.OmzetKor:C}. Deze omzet geeft u niet aan in de btw-aangifte."
        : null;

    public string AantalTekst => Overzicht is { } o
        ? $"{o.Facturen.Count} {(o.Facturen.Count == 1 ? "factuur" : "facturen en creditfacturen")} met een factuurdatum van " +
          $"{o.Van:d-M-yyyy} t/m {o.Tot:d-M-yyyy}."
        : "";

    public override async Task LaadAsync()
    {
        _bedrijfsnaam = (await Db.Bedrijfsprofielen.AsNoTracking().SingleAsync()).Bedrijfsnaam;
        _groepSoorten = await Db.BtwGroepen.AsNoTracking().ToDictionaryAsync(g => g.Id, g => g.Soort);
        _facturen = await Db.Facturen.AsNoTracking().Include(f => f.Regels)
            .Where(f => f.Status == FactuurStatus.Definitief).ToListAsync();

        var vandaag = DateTime.Today;
        var huidigKwartaal = (vandaag.Month - 1) / 3 + 1;
        // In de eerste maand van een kwartaal is meestal de aangifte over het vorige kwartaal aan de beurt.
        var (jaar, kwartaal) = vandaag.Month % 3 == 1
            ? huidigKwartaal == 1 ? (vandaag.Year - 1, 4) : (vandaag.Year, huidigKwartaal - 1)
            : (vandaag.Year, huidigKwartaal);

        Jaren = _facturen.Select(f => f.Factuurdatum.Year).Append(vandaag.Year).Append(jaar)
            .Distinct().OrderDescending().ToList();
        GekozenJaar = jaar;
        GekozenKwartaal = kwartaal;
        _geladen = true;
        Bereken();
    }

    private void Bereken()
    {
        if (!_geladen || GekozenJaar == 0)
            return;
        var (van, tot) = BtwRapportage.Periode(GekozenJaar, GekozenKwartaal == 0 ? null : GekozenKwartaal);
        Overzicht = BtwRapportage.Bereken(_facturen, _groepSoorten, van, tot);
        OnPropertyChanged(nameof(PeriodeNaam));
        Melding = null;
    }

    private bool HeeftOverzicht() => Overzicht is not null;

    [RelayCommand(CanExecute = nameof(HeeftOverzicht))]
    private async Task ExporteerAsync()
    {
        if (Overzicht is not { } overzicht)
            return;

        var map = Path.Combine(opslag.DataMap, "Btw-overzichten");
        Directory.CreateDirectory(map);
        var pad = dialogen.KiesOpslaanAls("Btw-overzicht opslaan", "Excel-werkmap (*.xlsx)|*.xlsx",
            $"Btw-overzicht {PeriodeNaam}.xlsx", map);
        if (pad is null)
            return;

        try
        {
            await Task.Run(() =>
            {
                using var bestand = File.Create(pad);
                BtwExcel.Exporteer(overzicht, _bedrijfsnaam, PeriodeNaam, bestand);
            });
            ToonSucces($"Het overzicht is opgeslagen als {pad}");
            FactuurDocumenten.Open(pad);
        }
        catch (IOException ex)
        {
            ToonFouten([$"Het bestand kon niet worden opgeslagen. Is het nog geopend in Excel? ({ex.Message})"]);
        }
    }
}
