using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Windows.Data;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using FactuurWizard.App.Services;
using FactuurWizard.Core.Model;
using FactuurWizard.Core.Services;
using FactuurWizard.Data;
using Microsoft.EntityFrameworkCore;

namespace FactuurWizard.App.ViewModels;

public partial class KlantenViewModel : PaginaViewModel
{
    private const int EersteKlantnummer = 1001;
    private readonly DialoogService _dialogen;

    public KlantenViewModel(IDbContextFactory<FactuurWizardDbContext> dbFactory, DialoogService dialogen)
        : base(dbFactory)
    {
        _dialogen = dialogen;
        KlantenView = CollectionViewSource.GetDefaultView(Klanten);
        KlantenView.Filter = o => o is Klant k && (ToonGearchiveerd || !k.Gearchiveerd) && PastBijZoekterm(k);
        KlantenView.SortDescriptions.Add(new SortDescription(nameof(Klant.Naam), ListSortDirection.Ascending));
    }

    public override string Titel => "Klanten";

    public ObservableCollection<Klant> Klanten { get; } = [];
    public ICollectionView KlantenView { get; }

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(VerwijderCommand))]
    [NotifyPropertyChangedFor(nameof(IsBedrijf), nameof(HeeftFactuuradres), nameof(NaamLabel))]
    public partial Klant? Geselecteerd { get; set; }

    [ObservableProperty]
    public partial bool ToonGearchiveerd { get; set; }

    [ObservableProperty]
    public partial string Zoekterm { get; set; } = "";

    partial void OnToonGearchiveerdChanged(bool value) => KlantenView.Refresh();
    partial void OnZoektermChanged(string value) => KlantenView.Refresh();

    private bool PastBijZoekterm(Klant k) =>
        string.IsNullOrWhiteSpace(Zoekterm) ||
        k.Klantnummer.ToString().Contains(Zoekterm) ||
        k.Naam.Contains(Zoekterm, StringComparison.OrdinalIgnoreCase) ||
        k.Contactpersoon.Contains(Zoekterm, StringComparison.OrdinalIgnoreCase) ||
        k.Adres.Plaats.Contains(Zoekterm, StringComparison.OrdinalIgnoreCase) ||
        k.Email.Contains(Zoekterm, StringComparison.OrdinalIgnoreCase);

    public bool IsBedrijf
    {
        get => Geselecteerd?.Soort == KlantSoort.Bedrijf;
        set
        {
            if (Geselecteerd is null || IsBedrijf == value)
                return;
            Geselecteerd.Soort = value ? KlantSoort.Bedrijf : KlantSoort.Particulier;
            OnPropertyChanged();
            OnPropertyChanged(nameof(NaamLabel));
        }
    }

    public string NaamLabel => IsBedrijf ? "Bedrijfsnaam *" : "Naam *";

    public bool HeeftFactuuradres
    {
        get => Geselecteerd?.Factuuradres is not null;
        set
        {
            if (Geselecteerd is null || HeeftFactuuradres == value)
                return;
            Geselecteerd.Factuuradres = value ? new Adres { Land = Geselecteerd.Adres.Land } : null;
            OnPropertyChanged();
            OnPropertyChanged(nameof(Geselecteerd));
        }
    }

    public override async Task LaadAsync()
    {
        Klanten.Clear();
        foreach (var klant in await Db.Klanten.ToListAsync())
            Klanten.Add(klant);
        Geselecteerd = KlantenView.Cast<Klant>().FirstOrDefault();
    }

    [RelayCommand]
    private async Task NieuwAsync()
    {
        // Ook gearchiveerde en eerder verwijderde nummers tellen mee, zodat een nummer nooit hergebruikt wordt.
        var hoogsteInDb = await Db.Klanten.Select(k => (int?)k.Klantnummer).MaxAsync() ?? 0;
        var hoogsteLokaal = Klanten.Select(k => k.Klantnummer).DefaultIfEmpty(0).Max();
        var klant = new Klant
        {
            Klantnummer = Math.Max(EersteKlantnummer, Math.Max(hoogsteInDb, hoogsteLokaal) + 1),
            Soort = KlantSoort.Particulier,
        };
        Db.Klanten.Add(klant);
        Klanten.Add(klant);
        Zoekterm = "";
        Geselecteerd = klant;
    }

    [RelayCommand(CanExecute = nameof(IsGeselecteerd))]
    private async Task VerwijderAsync()
    {
        if (Geselecteerd is not { } klant)
            return;

        var naam = string.IsNullOrWhiteSpace(klant.Naam) ? $"klant {klant.Klantnummer}" : klant.Naam;
        var heeftFacturen = klant.Id != 0 && await Db.Facturen.AnyAsync(f => f.KlantId == klant.Id);
        if (heeftFacturen)
        {
            if (!_dialogen.Bevestig($"Er zijn facturen voor {naam}, daarom kan deze klant niet verwijderd worden. " +
                                    "Wilt u de klant archiveren? De klant is dan niet meer te kiezen op nieuwe facturen, " +
                                    "maar de facturen blijven bewaard."))
                return;
            klant.Gearchiveerd = true;
        }
        else
        {
            if (!_dialogen.Bevestig($"Wilt u {naam} verwijderen?"))
                return;
            Db.Klanten.Remove(klant);
            Klanten.Remove(klant);
        }

        await Db.SaveChangesAsync();
        KlantenView.Refresh();
        Geselecteerd = KlantenView.Cast<Klant>().FirstOrDefault();
        ToonSucces(heeftFacturen ? "Klant gearchiveerd." : "Klant verwijderd.");
    }

    private bool IsGeselecteerd() => Geselecteerd is not null;

    [RelayCommand]
    private async Task OpslaanAsync()
    {
        var fouten = new List<string>();
        foreach (var k in Klanten)
        {
            Normaliseer(k);
            fouten.AddRange(Valideer(k).Select(f => $"Klant {k.Klantnummer}: {f}"));
        }

        if (fouten.Count > 0)
        {
            ToonFouten(fouten);
            return;
        }

        await Db.SaveChangesAsync();
        var geselecteerd = Geselecteerd;
        KlantenView.Refresh();
        Geselecteerd = null;
        Geselecteerd = geselecteerd;
        ToonSucces("Klantgegevens opgeslagen.");
    }

    private static void Normaliseer(Klant k)
    {
        k.Naam = k.Naam.Trim();
        k.Email = k.Email.Trim();
        k.Adres.Postcode = Validatie.FormatteerPostcode(k.Adres.Postcode);
        if (k.Factuuradres is not null)
            k.Factuuradres.Postcode = Validatie.FormatteerPostcode(k.Factuuradres.Postcode);
        if (k.Soort == KlantSoort.Bedrijf)
        {
            k.KvkNummer = Validatie.Compact(k.KvkNummer);
            k.BtwNummer = Validatie.Compact(k.BtwNummer).Replace(".", "");
        }
    }

    private static IEnumerable<string> Valideer(Klant k)
    {
        if (k.Naam.Length == 0)
            yield return k.Soort == KlantSoort.Bedrijf ? "vul de bedrijfsnaam in." : "vul de naam in.";
        if (k.Email.Length > 0 && !Validatie.IsGeldigEmailadres(k.Email))
            yield return "het e-mailadres is niet geldig.";
        if (IsOngeldigePostcode(k.Adres))
            yield return "de postcode is niet geldig (formaat 1234 AB).";
        if (k.Factuuradres is not null && IsOngeldigePostcode(k.Factuuradres))
            yield return "de postcode van het factuuradres is niet geldig.";
        if (k.Soort == KlantSoort.Bedrijf && k.KvkNummer.Length > 0 && !Validatie.IsGeldigKvkNummer(k.KvkNummer))
            yield return "het KvK-nummer moet uit 8 cijfers bestaan.";
        if (k.Soort == KlantSoort.Bedrijf && k.BtwNummer.Length > 0 && !Validatie.IsGeldigBtwNummer(k.BtwNummer))
            yield return "het btw-nummer is niet geldig.";
        if (k.BetalingstermijnDagen is < 0 or > 365)
            yield return "de betalingstermijn moet tussen 0 en 365 dagen liggen.";
    }

    private static bool IsOngeldigePostcode(Adres a) =>
        a.Postcode.Length > 0 && a.Land.Trim() == "Nederland" && !Validatie.IsGeldigePostcode(a.Postcode);
}
