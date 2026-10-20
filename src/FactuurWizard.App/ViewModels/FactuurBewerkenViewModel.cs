using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using FactuurWizard.App.Services;
using FactuurWizard.Core.Model;
using FactuurWizard.Core.Services;
using FactuurWizard.Data;
using FactuurWizard.Data.Services;
using Microsoft.EntityFrameworkCore;

namespace FactuurWizard.App.ViewModels;

public sealed record BtwRegelWeergave(string Omschrijving, decimal Grondslag, decimal Btw);

public partial class FactuurBewerkenViewModel : PaginaViewModel
{
    private readonly DialoogService _dialogen;
    private readonly Navigatie _navigatie;

    private int? _factuurId;
    private Bedrijfsprofiel _profiel = new();
    private Dictionary<int, BtwGroep> _groepen = [];
    private bool _nogNietOpgeslagen;
    private bool _verlaten;

    // Bij een creditfactuur volgt de btw de originele factuur.
    private DateOnly _origineleFactuurdatum;
    private bool _origineleKor;
    private string? _origineelNummer;

    public FactuurBewerkenViewModel(
        IDbContextFactory<FactuurWizardDbContext> dbFactory, DialoogService dialogen, Navigatie navigatie)
        : base(dbFactory)
    {
        _dialogen = dialogen;
        _navigatie = navigatie;
    }

    /// <summary>Kies de te openen factuur; null maakt een nieuwe conceptfactuur.</summary>
    public void Initialiseer(int? factuurId) => _factuurId = factuurId;

    public Factuur Factuur { get; private set; } = new();
    public ObservableCollection<FactuurregelItem> Regels { get; } = [];

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(VerwijderRegelCommand), nameof(RegelOmhoogCommand), nameof(RegelOmlaagCommand))]
    public partial FactuurregelItem? GeselecteerdeRegel { get; set; }

    [ObservableProperty]
    public partial IReadOnlyList<Klant> Klanten { get; set; } = [];

    [ObservableProperty]
    public partial IReadOnlyList<Keuze<int>> BtwGroepen { get; set; } = [];

    [ObservableProperty]
    public partial IReadOnlyList<Artikel> Artikelen { get; set; } = [];

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(VoegArtikelToeCommand))]
    public partial Artikel? GekozenArtikel { get; set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(BtwRegels))]
    public partial FactuurTotalen Totalen { get; set; } = FactuurBerekening.Bereken([]);

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(BtwRegels))]
    public partial bool KorVanToepassing { get; set; }

    public override string Titel =>
        (IsCredit ? "Creditfactuur" : "Factuur") + (IsConcept ? " (concept)" : $" {Factuur.Factuurnummer}");

    public bool IsConcept => Factuur.Status == FactuurStatus.Concept;
    public bool IsDefinitief => !IsConcept;
    public bool IsCredit => Factuur.Soort == FactuurSoort.Creditfactuur;
    public bool KanCrediteren => IsDefinitief && !IsCredit;

    public string? CreditInfo => IsCredit && _origineelNummer is not null
        ? $"Creditering van factuur {_origineelNummer}. Btw-tarieven volgen de originele factuur."
        : null;

    public override bool HeeftWijzigingen =>
        !_verlaten && (base.HeeftWijzigingen || (_nogNietOpgeslagen && (Regels.Count > 0 || GekozenKlant is not null)));

    public IEnumerable<BtwRegelWeergave> BtwRegels => KorVanToepassing
        ? [new BtwRegelWeergave("Geen btw (kleineondernemersregeling)", Totalen.TotaalExclBtw, 0m)]
        : Totalen.PerTarief.Select(s => new BtwRegelWeergave($"Btw {s.Percentage:0.##}%", s.Grondslag, s.Btw));

    public Klant? GekozenKlant
    {
        get => Factuur.Klant;
        set
        {
            if (ReferenceEquals(Factuur.Klant, value))
                return;
            Factuur.Klant = value;
            Factuur.KlantId = value?.Id ?? 0;
            if (value is not null && IsConcept && !IsCredit)
                Betalingstermijn = value.BetalingstermijnDagen ?? _profiel.StandaardBetalingstermijnDagen;
            OnPropertyChanged();
            OnPropertyChanged(nameof(KlantNaam));
            OnPropertyChanged(nameof(KlantAdres));
        }
    }

    /// <summary>Klantgegevens zoals ze op de factuur komen; bij een definitieve factuur de vastgelegde momentopname.</summary>
    private PartijGegevens? KlantOpFactuur =>
        IsDefinitief ? Factuur.KlantGegevens : Factuur.Klant is { } k ? PartijGegevens.Van(k) : null;

    public string KlantNaam => KlantOpFactuur?.Naam ?? "";

    public string KlantAdres
    {
        get
        {
            if (KlantOpFactuur is not { } k)
                return "";
            var a = k.Adres;
            var regels = new[]
            {
                k.Contactpersoon.Length > 0 ? $"t.a.v. {k.Contactpersoon}" : "",
                $"{a.Straat} {a.Huisnummer}".Trim(),
                $"{a.Postcode} {a.Plaats}".Trim(),
                a.Land is "" or "Nederland" ? "" : a.Land,
            };
            return string.Join(Environment.NewLine, regels.Where(r => r.Length > 0));
        }
    }

    public DateTime? Factuurdatum
    {
        get => Factuur.Factuurdatum.ToDateTime(TimeOnly.MinValue);
        set
        {
            if (value is null || DateOnly.FromDateTime(value.Value) == Factuur.Factuurdatum)
                return;
            Factuur.Factuurdatum = DateOnly.FromDateTime(value.Value);
            OnPropertyChanged();
            OnPropertyChanged(nameof(Vervaldatum));
            Herbereken();
        }
    }

    public DateTime? Leverdatum
    {
        get => Factuur.Leverdatum?.ToDateTime(TimeOnly.MinValue);
        set
        {
            Factuur.Leverdatum = value is null ? null : DateOnly.FromDateTime(value.Value);
            OnPropertyChanged();
        }
    }

    public DateTime? LeverperiodeEind
    {
        get => Factuur.LeverperiodeEind?.ToDateTime(TimeOnly.MinValue);
        set
        {
            Factuur.LeverperiodeEind = value is null ? null : DateOnly.FromDateTime(value.Value);
            OnPropertyChanged();
        }
    }

    public int Betalingstermijn
    {
        get => Factuur.BetalingstermijnDagen;
        set
        {
            if (Factuur.BetalingstermijnDagen == value)
                return;
            Factuur.BetalingstermijnDagen = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(Vervaldatum));
        }
    }

    public DateOnly Vervaldatum => Factuur.Vervaldatum;

    public string Opmerking
    {
        get => Factuur.Opmerking;
        set
        {
            Factuur.Opmerking = value;
            OnPropertyChanged();
        }
    }

    public override async Task LaadAsync()
    {
        _profiel = await Db.Bedrijfsprofielen.SingleAsync();
        var groepen = await Db.BtwGroepen.Include(g => g.Tarieven).OrderBy(g => g.Soort).ToListAsync();
        _groepen = groepen.ToDictionary(g => g.Id);
        BtwGroepen = groepen.Select(g => new Keuze<int>(g.Id, g.Naam)).ToList();
        Artikelen = await Db.Artikelen.Where(a => !a.Gearchiveerd).OrderBy(a => a.Omschrijving).ToListAsync();

        if (_factuurId is { } id)
        {
            Factuur = await Db.Facturen.Include(f => f.Regels).Include(f => f.Klant).SingleAsync(f => f.Id == id);
            if (Factuur.OrigineleFactuurId is { } origineelId)
            {
                var origineel = await Db.Facturen.AsNoTracking().SingleAsync(f => f.Id == origineelId);
                _origineleFactuurdatum = origineel.Factuurdatum;
                _origineleKor = origineel.KorToegepast;
                _origineelNummer = origineel.Factuurnummer;
            }
        }
        else
        {
            var briefpapier = await Db.Briefpapieren.OrderByDescending(b => b.IsStandaard).Select(b => (int?)b.Id).FirstOrDefaultAsync();
            Factuur = new Factuur
            {
                Factuurdatum = DateOnly.FromDateTime(DateTime.Today),
                BetalingstermijnDagen = _profiel.StandaardBetalingstermijnDagen,
                BriefpapierId = briefpapier,
                Taal = _profiel.Taal,
                Valuta = _profiel.Valuta,
            };
            _nogNietOpgeslagen = true;
        }

        await LaadKlantenAsync();

        Regels.Clear();
        foreach (var regel in Factuur.Regels.OrderBy(r => r.Volgorde))
            Regels.Add(new FactuurregelItem(regel, Herbereken));

        Herbereken();
        OnPropertyChanged(string.Empty);
    }

    private async Task LaadKlantenAsync()
    {
        var huidigeId = Factuur.KlantId;
        var klanten = await Db.Klanten.Where(k => !k.Gearchiveerd || k.Id == huidigeId).ToListAsync();
        klanten.AddRange(Db.Klanten.Local.Where(k => k.Id == 0 && !klanten.Contains(k)));
        Klanten = klanten.OrderBy(k => k.Naam, StringComparer.CurrentCultureIgnoreCase).ToList();
    }

    /// <summary>Bepaalt de btw-percentages (alleen bij een concept) en berekent de totalen opnieuw.</summary>
    private void Herbereken()
    {
        if (IsConcept)
        {
            var datum = IsCredit ? _origineleFactuurdatum : Factuur.Factuurdatum;
            KorVanToepassing = IsCredit ? _origineleKor : _profiel.KorVanToepassing(datum);
            foreach (var item in Regels)
            {
                item.Regel.BtwPercentage = KorVanToepassing || !_groepen.TryGetValue(item.BtwGroepId, out var groep)
                    ? 0m
                    : groep.Tarieven.FirstOrDefault(t => t.GeldigOp(datum))?.Percentage ?? 0m;
                item.VerversBtw();
            }
        }
        else
        {
            KorVanToepassing = Factuur.KorToegepast;
        }

        Totalen = FactuurBerekening.Bereken(Factuur.Regels);
    }

    private void VoegRegelToe(Factuurregel regel)
    {
        regel.Volgorde = Regels.Count + 1;
        Factuur.Regels.Add(regel);
        var item = new FactuurregelItem(regel, Herbereken);
        Regels.Add(item);
        GeselecteerdeRegel = item;
        Herbereken();
    }

    [RelayCommand]
    private void VoegVrijeRegelToe()
    {
        var standaardGroep = _groepen.Values.FirstOrDefault(g => g.Soort == BtwSoort.Hoog) ?? _groepen.Values.First();
        VoegRegelToe(new Factuurregel { Soort = RegelSoort.Vrij, Aantal = 1, BtwGroepId = standaardGroep.Id });
    }

    [RelayCommand(CanExecute = nameof(IsArtikelGekozen))]
    private void VoegArtikelToe()
    {
        if (GekozenArtikel is not { } a)
            return;
        VoegRegelToe(new Factuurregel
        {
            Soort = a.Soort == ArtikelSoort.Dienst ? RegelSoort.Dienst : RegelSoort.Product,
            ArtikelId = a.Id,
            Artikelnummer = a.Artikelnummer,
            Omschrijving = a.Omschrijving,
            Aantal = 1,
            Eenheid = a.Eenheid,
            PrijsExclBtw = a.PrijsExclBtw,
            BtwGroepId = a.BtwGroepId,
        });
        GekozenArtikel = null;
    }

    private bool IsArtikelGekozen() => GekozenArtikel is not null;

    [RelayCommand(CanExecute = nameof(IsRegelGeselecteerd))]
    private void VerwijderRegel()
    {
        if (GeselecteerdeRegel is not { } item)
            return;
        var index = Regels.IndexOf(item);
        Regels.Remove(item);
        Factuur.Regels.Remove(item.Regel);
        Hernummer();
        GeselecteerdeRegel = Regels.Count == 0 ? null : Regels[Math.Min(index, Regels.Count - 1)];
        Herbereken();
    }

    [RelayCommand(CanExecute = nameof(IsRegelGeselecteerd))]
    private void RegelOmhoog() => Verplaats(-1);

    [RelayCommand(CanExecute = nameof(IsRegelGeselecteerd))]
    private void RegelOmlaag() => Verplaats(1);

    private void Verplaats(int richting)
    {
        if (GeselecteerdeRegel is not { } item)
            return;
        var van = Regels.IndexOf(item);
        var naar = van + richting;
        if (naar < 0 || naar >= Regels.Count)
            return;
        Regels.Move(van, naar);
        Hernummer();
        GeselecteerdeRegel = item;
    }

    private bool IsRegelGeselecteerd() => GeselecteerdeRegel is not null && IsConcept;

    private void Hernummer()
    {
        for (var i = 0; i < Regels.Count; i++)
            Regels[i].Regel.Volgorde = i + 1;
    }

    [RelayCommand]
    private async Task NieuweKlantAsync()
    {
        var klant = new Klant { Klantnummer = await Klantnummering.VolgendAsync(Db) };
        if (!_dialogen.NieuweKlant(klant))
            return;
        Db.Klanten.Add(klant);
        await LaadKlantenAsync();
        GekozenKlant = klant;
    }

    [RelayCommand]
    private async Task OpslaanAsync()
    {
        if (await BewaarConceptAsync())
            ToonSucces("Concept opgeslagen.");
    }

    private async Task<bool> BewaarConceptAsync()
    {
        var fouten = new List<string>();
        if (GekozenKlant is null)
            fouten.Add("Kies een klant.");
        if (Betalingstermijn is < 0 or > 365)
            fouten.Add("De betalingstermijn moet tussen 0 en 365 dagen liggen.");
        if (Factuur.Leverdatum is { } van && Factuur.LeverperiodeEind is { } tot && tot < van)
            fouten.Add("Het einde van de leverperiode ligt vóór de leverdatum.");
        if (fouten.Count > 0)
        {
            ToonFouten(fouten);
            return false;
        }

        Hernummer();
        Factuur.TotaalExclBtw = Totalen.TotaalExclBtw;
        Factuur.TotaalBtw = Totalen.TotaalBtw;
        Factuur.TotaalInclBtw = Totalen.TotaalInclBtw;
        if (Db.Entry(Factuur).State == EntityState.Detached)
            Db.Facturen.Add(Factuur);

        await Db.SaveChangesAsync();
        _nogNietOpgeslagen = false;
        return true;
    }

    [RelayCommand]
    private async Task MaakDefinitiefAsync()
    {
        if (!await BewaarConceptAsync())
            return;

        var jaar = Factuur.Factuurdatum.Year;
        var datums = await Db.Facturen
            .Where(f => f.Status == FactuurStatus.Definitief && f.Jaar == jaar)
            .Select(f => f.Factuurdatum)
            .ToListAsync();
        var vandaag = DateOnly.FromDateTime(DateTime.Today);
        var controle = FactuurControle.VoorDefinitief(
            Factuur, GekozenKlant!, _profiel, datums.Count > 0 ? datums.Max() : null, vandaag);

        if (!controle.IsGoed)
        {
            ToonFouten(["De factuur kan nog niet definitief gemaakt worden:", .. controle.Fouten.Select(f => "• " + f)]);
            return;
        }

        var vraag = controle.Waarschuwingen.Count > 0
            ? "Let op:\n" + string.Join("\n", controle.Waarschuwingen.Select(w => "• " + w)) + "\n\n"
            : "";
        vraag += "Na het definitief maken krijgt de factuur een nummer en kan hij niet meer gewijzigd worden. " +
                 "Correcties gaan daarna via een creditfactuur.\n\nWilt u doorgaan?";
        if (!_dialogen.Bevestig(vraag, "Factuur definitief maken"))
            return;

        try
        {
            await new FactuurService(Db).MaakDefinitiefAsync(Factuur.Id, DateTime.Now);
        }
        catch (Exception ex) when (ex is FactuurException or InvalidOperationException)
        {
            ToonFouten([ex.Message]);
            return;
        }

        Herbereken();
        GeselecteerdeRegel = null;
        OnPropertyChanged(string.Empty);
        ToonSucces($"{(IsCredit ? "Creditfactuur" : "Factuur")} {Factuur.Factuurnummer} is definitief gemaakt.");
    }

    [RelayCommand]
    private async Task VerwijderConceptAsync()
    {
        if (!IsConcept || !_dialogen.Bevestig("Wilt u dit concept verwijderen?"))
            return;

        if (Db.Entry(Factuur).State != EntityState.Detached)
        {
            Db.Facturen.Remove(Factuur);
            await Db.SaveChangesAsync();
        }
        _verlaten = true;
        await _navigatie.OpenAsync<FacturenViewModel>();
    }

    [RelayCommand]
    private async Task MaakCreditfactuurAsync()
    {
        if (!KanCrediteren)
            return;

        var bestaand = await Db.Facturen.Where(f => f.OrigineleFactuurId == Factuur.Id).Select(f => f.Factuurnummer).ToListAsync();
        var vraag = bestaand.Count > 0
            ? $"Er bestaat al een creditfactuur voor deze factuur ({string.Join(", ", bestaand.Select(n => n ?? "concept"))}). " +
              "Wilt u nog een creditfactuur maken?"
            : $"Wilt u een creditfactuur maken voor factuur {Factuur.Factuurnummer}? U kunt de regels daarna nog aanpassen, " +
              "bijvoorbeeld voor een gedeeltelijke creditering.";
        if (!_dialogen.Bevestig(vraag, "Creditfactuur"))
            return;

        var credit = await new FactuurService(Db).MaakCreditfactuurAsync(Factuur.Id, DateOnly.FromDateTime(DateTime.Today));
        await _navigatie.OpenAsync<FactuurBewerkenViewModel>(p => p.Initialiseer(credit.Id));
    }

    [RelayCommand]
    private Task TerugAsync() => _navigatie.OpenAsync<FacturenViewModel>();
}
