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

public sealed record HerinneringItem(Herinnering Herinnering)
{
    public string Omschrijving =>
        $"{Herinnering.Datum:d-M-yyyy}  {(Herinnering.Niveau >= 2 ? "2e herinnering" : "1e herinnering")}, " +
        $"{Herinnering.BetalingstermijnDagen} dagen";

    public override string ToString() => Omschrijving;
}

public partial class FactuurBewerkenViewModel : PaginaViewModel
{
    private readonly DialoogService _dialogen;
    private readonly Navigatie _navigatie;
    private readonly FactuurDocumenten _documenten;
    private readonly Mailer _mailer;

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
        IDbContextFactory<FactuurWizardDbContext> dbFactory, DialoogService dialogen, Navigatie navigatie,
        FactuurDocumenten documenten, Mailer mailer)
        : base(dbFactory)
    {
        _dialogen = dialogen;
        _navigatie = navigatie;
        _documenten = documenten;
        _mailer = mailer;
    }

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(VoorbeeldCommand), nameof(PdfOpenenCommand), nameof(EmailenCommand))]
    public partial bool IsBezig { get; set; }

    private bool IsNietBezig() => !IsBezig;

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
                a.StraatRegel,
                a.PostcodePlaatsRegel,
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
            Factuur = await Db.Facturen.Include(f => f.Regels).Include(f => f.Klant).Include(f => f.Herinneringen)
                .SingleAsync(f => f.Id == id);
            _gecrediteerd = await new FactuurService(Db).GecrediteerdAsync(id);
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
        VerversHerinneringen();

        Herbereken();
        OnPropertyChanged(string.Empty);
    }

    // ---------- Betaling en herinneringen ----------

    private decimal _gecrediteerd;

    public ObservableCollection<HerinneringItem> Herinneringen { get; } = [];

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(HerinneringOpenenCommand), nameof(HerinneringEmailenCommand))]
    public partial HerinneringItem? GeselecteerdeHerinnering { get; set; }

    [ObservableProperty]
    public partial DateTime? BetaaldOpInvoer { get; set; } = DateTime.Today;

    [ObservableProperty]
    public partial int HerinneringTermijn { get; set; } = 7;

    private BetaalStatus Betaalstatus => Factuur.BetaalStatusOp(DateOnly.FromDateTime(DateTime.Today), _gecrediteerd);

    public bool ToonBetaling => IsDefinitief && !IsCredit;
    public bool ToonOpmerking => IsConcept || Factuur.Opmerking.Trim().Length > 0;
    public bool IsBetaald => Betaalstatus == BetaalStatus.Betaald;
    public bool KanBetaaldMarkeren => Betaalstatus is BetaalStatus.Openstaand or BetaalStatus.Vervallen;
    public bool KanHerinneren => KanBetaaldMarkeren;

    public string BetaalstatusTekst => Betaalstatus switch
    {
        BetaalStatus.Betaald => $"Betaald op {Factuur.BetaaldOp:d-M-yyyy}.",
        BetaalStatus.Gecrediteerd => "Volledig gecrediteerd; er staat niets meer open.",
        BetaalStatus.Vervallen => $"Vervallen sinds {Factuur.Vervaldatum:d-M-yyyy}. Openstaand: {Openstaand:C}.",
        BetaalStatus.Openstaand => $"Openstaand: {Openstaand:C}, te betalen vóór {Factuur.Vervaldatum:d-M-yyyy}.",
        _ => "",
    };

    private decimal Openstaand => Factuur.OpenstaandBedrag(_gecrediteerd);

    private void VerversHerinneringen()
    {
        Herinneringen.Clear();
        foreach (var h in Factuur.Herinneringen.OrderBy(h => h.Datum).ThenBy(h => h.Id))
            Herinneringen.Add(new HerinneringItem(h));
        GeselecteerdeHerinnering = Herinneringen.LastOrDefault();
    }

    private void VerversBetaling()
    {
        foreach (var naam in new[] { nameof(IsBetaald), nameof(KanBetaaldMarkeren), nameof(KanHerinneren), nameof(BetaalstatusTekst) })
            OnPropertyChanged(naam);
    }

    [RelayCommand]
    private Task MarkeerBetaaldAsync() => VoerUitAsync(async () =>
    {
        if (BetaaldOpInvoer is not { } datum)
        {
            ToonFouten(["Vul de betaaldatum in."]);
            return;
        }
        await new FactuurService(Db).ZetBetaaldAsync(Factuur.Id, DateOnly.FromDateTime(datum));
        VerversBetaling();
        ToonSucces($"Factuur {Factuur.Factuurnummer} is gemarkeerd als betaald.");
    });

    [RelayCommand]
    private Task BetalingOngedaanAsync() => VoerUitAsync(async () =>
    {
        if (!_dialogen.Bevestig("Wilt u de betaling van deze factuur ongedaan maken?"))
            return;
        await new FactuurService(Db).ZetBetaaldAsync(Factuur.Id, null);
        VerversBetaling();
        ToonSucces("De betaling is ongedaan gemaakt.");
    });

    [RelayCommand]
    private Task MaakHerinneringAsync() => VoerUitAsync(async () =>
    {
        var vandaag = DateOnly.FromDateTime(DateTime.Today);
        var niveau = Math.Min(2, Factuur.Herinneringen.Count + 1);
        var soort = niveau == 1 ? "een betalingsherinnering" : "een tweede herinnering";
        var vraag = Betaalstatus == BetaalStatus.Openstaand
            ? $"De vervaldatum ({Factuur.Vervaldatum:d-M-yyyy}) is nog niet verstreken. Wilt u toch {soort} maken?"
            : $"Wilt u {soort} maken met een betalingstermijn van {HerinneringTermijn} dagen?";
        if (!_dialogen.Bevestig(vraag, "Herinnering"))
            return;

        var herinnering = await new FactuurService(Db).MaakHerinneringAsync(Factuur.Id, vandaag, HerinneringTermijn);
        var pad = await _documenten.ZorgVoorHerinneringPdfAsync(Db, Factuur, herinnering, Openstaand);
        VerversHerinneringen();
        FactuurDocumenten.Open(pad);
        ToonSucces($"De herinnering is opgeslagen als {pad}");
    });

    private bool IsHerinneringGeselecteerd() => GeselecteerdeHerinnering is not null;

    [RelayCommand(CanExecute = nameof(IsHerinneringGeselecteerd))]
    private Task HerinneringOpenenAsync() => VoerUitAsync(async () =>
    {
        FactuurDocumenten.Open(await _documenten.ZorgVoorHerinneringPdfAsync(Db, Factuur, GeselecteerdeHerinnering!.Herinnering, Openstaand));
    });

    [RelayCommand(CanExecute = nameof(IsHerinneringGeselecteerd))]
    private Task HerinneringEmailenAsync() => VoerUitAsync(async () =>
    {
        var herinnering = GeselecteerdeHerinnering!.Herinnering;
        var herinneringPdf = await _documenten.ZorgVoorHerinneringPdfAsync(Db, Factuur, herinnering, Openstaand);
        var factuurPdf = await _documenten.ZorgVoorPdfAsync(Db, Factuur);

        var teksten = await Db.Tekstsjablonen.Where(t => t.Taal == Factuur.Taal).ToDictionaryAsync(t => t.Soort, t => t.Tekst);
        var waarden = Plaatshouders.VoorFactuur(Factuur, _profiel, herinnering.BetalingstermijnDagen);
        waarden["TotaalInclBtw"] = Openstaand.ToString("C");
        var onderwerp = Plaatshouders.Vul(teksten.GetValueOrDefault(TekstSoort.HerinneringEmailOnderwerp, ""), waarden);
        var tekst = Plaatshouders.Vul(teksten.GetValueOrDefault(TekstSoort.HerinneringEmailTekst, ""), waarden);

        await MailAsync(Factuur.KlantGegevens.Email, onderwerp, tekst, herinneringPdf, factuurPdf);
    });

    [RelayCommand]
    private Task KopieAsync() => VoerUitAsync(async () =>
    {
        if (HeeftWijzigingen && !await BewaarConceptAsync())
            return;
        var kopie = await new FactuurService(Db).MaakKopieAsync(Factuur.Id, DateOnly.FromDateTime(DateTime.Today),
            _profiel.StandaardBetalingstermijnDagen);
        await _navigatie.OpenAsync<FactuurBewerkenViewModel>(p => p.Initialiseer(kopie.Id));
    });

    private async Task MailAsync(string aan, string onderwerp, string tekst, params IReadOnlyList<string> bijlagen)
    {
        switch (await _mailer.NieuwBerichtAsync(aan, onderwerp, tekst, bijlagen))
        {
            case MailResultaat.Geopend:
                ToonSucces("Het bericht is geopend in uw mailprogramma.");
                break;
            case MailResultaat.Geannuleerd:
                ToonSucces("Het bericht is niet verstuurd.");
                break;
            case MailResultaat.ZonderBijlage:
                _dialogen.Informeer("Uw mailprogramma kon de bijlage(n) niet automatisch toevoegen. Er is een nieuw bericht " +
                                    "geopend en de PDF is geselecteerd in Verkenner: sleep het bestand naar het bericht.");
                break;
        }
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

        var soort = IsCredit ? "Creditfactuur" : "Factuur";
        try
        {
            IsBezig = true;
            var pad = await _documenten.ZorgVoorPdfAsync(Db, Factuur);
            ToonSucces($"{soort} {Factuur.Factuurnummer} is definitief gemaakt. De PDF is opgeslagen als {pad}");
        }
        catch (Exception ex)
        {
            Foutlog.Schrijf(ex);
            ToonFouten([$"{soort} {Factuur.Factuurnummer} is definitief gemaakt, maar de PDF kon niet worden gemaakt: " +
                        $"{ex.Message} Probeer het opnieuw met 'PDF openen'."]);
        }
        finally
        {
            IsBezig = false;
        }
    }

    /// <summary>Toont een concept als PDF met het watermerk "CONCEPT", zonder iets op te slaan.</summary>
    [RelayCommand(CanExecute = nameof(IsNietBezig))]
    private async Task VoorbeeldAsync()
    {
        if (GekozenKlant is null)
        {
            ToonFouten(["Kies eerst een klant."]);
            return;
        }
        await VoerUitAsync(async () =>
        {
            var invoer = await _documenten.MaakInvoerAsync(Db, Factuur);
            FactuurDocumenten.Open(await _documenten.MaakTijdelijkePdfAsync(invoer, "Concept " + KlantNaam));
        });
    }

    [RelayCommand(CanExecute = nameof(IsNietBezig))]
    private Task PdfOpenenAsync() => VoerUitAsync(async () =>
    {
        FactuurDocumenten.Open(await _documenten.ZorgVoorPdfAsync(Db, Factuur));
    });

    [RelayCommand(CanExecute = nameof(IsNietBezig))]
    private Task EmailenAsync() => VoerUitAsync(async () =>
    {
        var pad = await _documenten.ZorgVoorPdfAsync(Db, Factuur);
        var teksten = await Db.Tekstsjablonen.Where(t => t.Taal == Factuur.Taal).ToDictionaryAsync(t => t.Soort, t => t.Tekst);
        var waarden = Plaatshouders.VoorFactuur(Factuur, _profiel);
        var onderwerp = Plaatshouders.Vul(teksten.GetValueOrDefault(TekstSoort.EmailOnderwerp, ""), waarden);
        var tekst = Plaatshouders.Vul(teksten.GetValueOrDefault(TekstSoort.EmailTekst, ""), waarden);
        await MailAsync(Factuur.KlantGegevens.Email, onderwerp, tekst, pad);
    });

    private async Task VoerUitAsync(Func<Task> actie)
    {
        try
        {
            IsBezig = true;
            await actie();
        }
        catch (Exception ex)
        {
            Foutlog.Schrijf(ex);
            ToonFouten([$"Dit is niet gelukt: {ex.Message}"]);
        }
        finally
        {
            IsBezig = false;
        }
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
