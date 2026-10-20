using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Threading;
using FactuurWizard.Core.Model;
using FactuurWizard.Data.Services;

namespace FactuurWizard.App.Views;

/// <summary>
/// Adresinvoer die bij een Nederlands adres straat en plaats opzoekt op postcode en huisnummer (BAG via PDOK).
/// Velden die de gebruiker zelf heeft aangepast, worden niet overschreven.
/// </summary>
public partial class AdresInvoer : UserControl
{
    private readonly DispatcherTimer _wachttijd = new() { Interval = TimeSpan.FromMilliseconds(500) };
    private int _zoekopdracht;
    private bool _bezigMetInvullen;
    private string? _automatischeStraat;
    private string? _automatischePlaats;
    private IReadOnlyList<AdresSuggestie> _gevonden = [];

    public AdresInvoer()
    {
        InitializeComponent();
        _wachttijd.Tick += async (_, _) => await ZoekAsync();
        DataContextChanged += (_, _) => Herstel();
        Unloaded += (_, _) => _wachttijd.Stop();
    }

    private Adres? Adres => DataContext as Adres;

    private void Herstel()
    {
        _wachttijd.Stop();
        _zoekopdracht++;
        _automatischeStraat = _automatischePlaats = null;
        _gevonden = [];
        ZetToevoegingen([]);
        ToonStatus(null);
    }

    private void Zoekveld_TextChanged(object sender, TextChangedEventArgs e)
    {
        if (_bezigMetInvullen)
            return;
        // Een oude melding hoort niet meer bij de nieuwe invoer.
        _zoekopdracht++;
        ToonStatus(null);
        _wachttijd.Stop();
        _wachttijd.Start();
    }

    private void Toevoeging_TextChanged(object sender, TextChangedEventArgs e)
    {
        if (!_bezigMetInvullen && _gevonden.Count > 0)
            ToonResultaat();
    }

    private async Task ZoekAsync()
    {
        _wachttijd.Stop();
        if (Adres is not { } adres || !adres.IsNederlands)
        {
            ToonStatus(null);
            return;
        }
        if (!Core.Services.Validatie.IsGeldigePostcode(adres.Postcode) || !int.TryParse(adres.Huisnummer.Trim(), out _))
        {
            _gevonden = [];
            ZetToevoegingen([]);
            ToonStatus(null);
            return;
        }

        var opdracht = ++_zoekopdracht;
        ToonStatus("Adres opzoeken…");
        var resultaat = await AdresZoeker.Standaard.ZoekAsync(adres.Postcode, adres.Huisnummer);
        if (opdracht != _zoekopdracht || !ReferenceEquals(adres, Adres))
            return; // De invoer is inmiddels veranderd.

        _gevonden = resultaat.Adressen;
        switch (resultaat.Status)
        {
            case ZoekStatus.Mislukt:
                ZetToevoegingen([]);
                ToonStatus("Opzoeken lukt nu niet (geen internetverbinding?). U kunt het adres zelf invullen.");
                return;
            case ZoekStatus.GeenAntwoord:
                ZetToevoegingen([]);
                ToonStatus("Geen adres gevonden: de postcode bestaat mogelijk niet, of de adresservice reageert niet. " +
                           "Controleer postcode en huisnummer, of vul het adres zelf in.");
                return;
            case ZoekStatus.NietGevonden:
                ZetToevoegingen([]);
                ToonStatus("Dit adres staat niet in de Basisregistratie Adressen. Controleer postcode en huisnummer, of vul het adres zelf in.");
                return;
        }

        var eerste = _gevonden[0];
        Vul(() =>
        {
            adres.Postcode = eerste.Postcode;
            if (string.IsNullOrWhiteSpace(adres.Straat) || adres.Straat == _automatischeStraat)
                adres.Straat = _automatischeStraat = eerste.Straat;
            if (string.IsNullOrWhiteSpace(adres.Plaats) || adres.Plaats == _automatischePlaats)
                adres.Plaats = _automatischePlaats = eerste.Plaats;
            // Bestaat er maar één variant en heeft die een toevoeging, dan is dat vrijwel zeker de bedoelde.
            if (_gevonden.Count == 1 && eerste.Toevoeging.Length > 0 && string.IsNullOrWhiteSpace(adres.Toevoeging))
                adres.Toevoeging = eerste.Toevoeging;
        });

        ZetToevoegingen(_gevonden.Select(a => a.Toevoeging).Where(t => t.Length > 0).ToList());
        ToonResultaat();
    }

    private void ToonResultaat()
    {
        if (Adres is not { } adres || _gevonden.Count == 0)
            return;

        var gekozen = _gevonden.FirstOrDefault(a => a.Toevoeging.Equals(adres.Toevoeging.Trim(), StringComparison.OrdinalIgnoreCase));
        if (gekozen is not null)
        {
            var voorbeeld = new Adres { Straat = gekozen.Straat, Huisnummer = gekozen.Huisnummer, Toevoeging = gekozen.Toevoeging };
            var afwijkend = !adres.Straat.Trim().Equals(gekozen.Straat, StringComparison.OrdinalIgnoreCase) ||
                            !adres.Plaats.Trim().Equals(gekozen.Plaats, StringComparison.OrdinalIgnoreCase);
            ToonStatus($"✓ {voorbeeld.StraatRegel}, {gekozen.Postcode} {gekozen.Plaats}" +
                       (afwijkend ? " – let op: de ingevulde straat of plaats wijkt hiervan af." : ""));
            return;
        }

        var opties = string.Join(", ", _gevonden.Select(a => a.Toevoeging.Length > 0 ? a.Toevoeging : "(geen)"));
        ToonStatus(adres.Toevoeging.Trim().Length == 0
            ? $"Op dit huisnummer zijn meerdere adressen. Kies de toevoeging: {opties}."
            : $"Toevoeging '{adres.Toevoeging.Trim()}' is niet bekend op dit adres. Mogelijk: {opties}.");
    }

    /// <summary>Wijzigt het adres vanuit code en ververst de velden, zonder een nieuwe zoekopdracht te starten.</summary>
    private void Vul(Action wijziging)
    {
        _bezigMetInvullen = true;
        try
        {
            wijziging();
            foreach (var veld in new FrameworkElement[] { PostcodeVeld, StraatVeld, PlaatsVeld })
                BindingOperations.GetBindingExpression(veld, TextBox.TextProperty)?.UpdateTarget();
            BindingOperations.GetBindingExpression(ToevoegingVeld, ComboBox.TextProperty)?.UpdateTarget();
        }
        finally
        {
            _bezigMetInvullen = false;
        }
    }

    /// <summary>
    /// Vervangt de keuzelijst van toevoegingen. Een bewerkbare ComboBox kan daarbij de ingetypte tekst
    /// wissen; die wordt daarom bewaard en zo nodig teruggezet.
    /// </summary>
    private void ZetToevoegingen(IReadOnlyList<string> toevoegingen)
    {
        var huidige = Adres?.Toevoeging;
        _bezigMetInvullen = true;
        try
        {
            ToevoegingVeld.ItemsSource = toevoegingen.Count > 0 ? toevoegingen : null;
            if (Adres is { } adres && huidige is not null && adres.Toevoeging != huidige)
                adres.Toevoeging = huidige;
            ToevoegingVeld.Text = huidige ?? "";
        }
        finally
        {
            _bezigMetInvullen = false;
        }
    }

    private void ToonStatus(string? tekst)
    {
        StatusTekst.Text = tekst ?? "";
        StatusTekst.Visibility = tekst is null ? Visibility.Collapsed : Visibility.Visible;
    }
}
