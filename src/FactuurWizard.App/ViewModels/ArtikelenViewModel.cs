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

public sealed record Keuze<T>(T Waarde, string Omschrijving);

public partial class ArtikelenViewModel : PaginaViewModel
{
    private readonly DialoogService _dialogen;

    public ArtikelenViewModel(IDbContextFactory<FactuurWizardDbContext> dbFactory, DialoogService dialogen)
        : base(dbFactory)
    {
        _dialogen = dialogen;
        ArtikelenView = CollectionViewSource.GetDefaultView(Artikelen);
        ArtikelenView.Filter = o => o is Artikel a && (ToonGearchiveerd || !a.Gearchiveerd) && PastBijZoekterm(a);
        ArtikelenView.SortDescriptions.Add(new SortDescription(nameof(Artikel.Artikelnummer), ListSortDirection.Ascending));
    }

    public override string Titel => "Producten en diensten";

    public ObservableCollection<Artikel> Artikelen { get; } = [];
    public ICollectionView ArtikelenView { get; }

    public IReadOnlyList<Keuze<ArtikelSoort>> Soorten { get; } =
        [new(ArtikelSoort.Product, "Product"), new(ArtikelSoort.Dienst, "Dienst")];

    public IReadOnlyList<string> Eenheden { get; } = ["stuk", "uur", "dag", "km", "maand", "jaar", "project", "m²"];

    [ObservableProperty]
    public partial IReadOnlyList<Keuze<int>> BtwGroepen { get; set; } = [];

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(VerwijderCommand))]
    public partial Artikel? Geselecteerd { get; set; }

    [ObservableProperty]
    public partial bool ToonGearchiveerd { get; set; }

    [ObservableProperty]
    public partial string Zoekterm { get; set; } = "";

    partial void OnToonGearchiveerdChanged(bool value) => ArtikelenView.Refresh();
    partial void OnZoektermChanged(string value) => ArtikelenView.Refresh();

    private bool PastBijZoekterm(Artikel a) =>
        string.IsNullOrWhiteSpace(Zoekterm) ||
        a.Artikelnummer.Contains(Zoekterm, StringComparison.OrdinalIgnoreCase) ||
        a.Omschrijving.Contains(Zoekterm, StringComparison.OrdinalIgnoreCase);

    public override async Task LaadAsync()
    {
        var vandaag = DateOnly.FromDateTime(DateTime.Today);
        var groepen = await Db.BtwGroepen.Include(g => g.Tarieven).OrderBy(g => g.Soort).ToListAsync();
        BtwGroepen = groepen
            .Select(g => new Keuze<int>(g.Id,
                g.Tarieven.FirstOrDefault(t => t.GeldigOp(vandaag)) is { } t ? $"{g.Naam} ({t.Percentage:0.##}%)" : g.Naam))
            .ToList();

        Artikelen.Clear();
        foreach (var artikel in await Db.Artikelen.ToListAsync())
            Artikelen.Add(artikel);
        Geselecteerd = ArtikelenView.Cast<Artikel>().FirstOrDefault();
    }

    [RelayCommand]
    private void Nieuw()
    {
        var artikel = new Artikel
        {
            Soort = ArtikelSoort.Product,
            Omschrijving = "Nieuw artikel",
            BtwGroepId = BtwGroepen.FirstOrDefault()?.Waarde ?? 0,
            Artikelnummer = VolgendArtikelnummer(),
        };
        Db.Artikelen.Add(artikel);
        Artikelen.Add(artikel);
        Zoekterm = "";
        Geselecteerd = artikel;
    }

    private string VolgendArtikelnummer()
    {
        var hoogste = Artikelen.Select(a => int.TryParse(a.Artikelnummer, out var n) ? n : 0).DefaultIfEmpty(1000).Max();
        return (hoogste + 1).ToString();
    }

    [RelayCommand(CanExecute = nameof(IsGeselecteerd))]
    private async Task VerwijderAsync()
    {
        if (Geselecteerd is not { } artikel)
            return;

        var inGebruik = artikel.Id != 0 && await Db.Factuurregels.AnyAsync(r => r.ArtikelId == artikel.Id);
        if (inGebruik)
        {
            if (!_dialogen.Bevestig($"'{artikel.Omschrijving}' staat op een of meer facturen en kan daarom niet " +
                                    "verwijderd worden. Wilt u het artikel archiveren? Het is dan niet meer te kiezen " +
                                    "op nieuwe facturen."))
                return;
            artikel.Gearchiveerd = true;
        }
        else
        {
            if (!_dialogen.Bevestig($"Wilt u '{artikel.Omschrijving}' verwijderen?"))
                return;
            Db.Artikelen.Remove(artikel);
            Artikelen.Remove(artikel);
        }

        await Db.SaveChangesAsync();
        ArtikelenView.Refresh();
        Geselecteerd = ArtikelenView.Cast<Artikel>().FirstOrDefault();
        ToonSucces(inGebruik ? "Artikel gearchiveerd." : "Artikel verwijderd.");
    }

    private bool IsGeselecteerd() => Geselecteerd is not null;

    [RelayCommand]
    private async Task OpslaanAsync()
    {
        var fouten = new List<string>();
        foreach (var a in Artikelen)
        {
            a.Artikelnummer = a.Artikelnummer.Trim();
            a.Omschrijving = a.Omschrijving.Trim();
            var naam = a.Artikelnummer.Length > 0 ? a.Artikelnummer : a.Omschrijving;
            if (a.Omschrijving.Length == 0)
                fouten.Add($"Artikel {naam}: vul een omschrijving in.");
            if (a.PrijsExclBtw < 0)
                fouten.Add($"Artikel {naam}: de prijs mag niet negatief zijn.");
            if (BtwGroepen.All(g => g.Waarde != a.BtwGroepId))
                fouten.Add($"Artikel {naam}: kies een btw-groep.");
        }

        fouten.AddRange(Artikelen
            .Where(a => a.Artikelnummer.Length > 0)
            .GroupBy(a => a.Artikelnummer, StringComparer.OrdinalIgnoreCase)
            .Where(g => g.Count() > 1)
            .Select(g => $"Artikelnummer {g.Key} komt meerdere keren voor."));

        if (fouten.Count > 0)
        {
            ToonFouten(fouten);
            return;
        }

        await Db.SaveChangesAsync();
        var geselecteerd = Geselecteerd;
        ArtikelenView.Refresh();
        Geselecteerd = geselecteerd;
        ToonSucces("Producten en diensten opgeslagen.");
    }
}
