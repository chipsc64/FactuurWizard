namespace FactuurWizard.Core.Model;

public class Factuur
{
    public int Id { get; set; }
    public FactuurSoort Soort { get; set; }
    public FactuurStatus Status { get; set; }

    /// <summary>Wordt pas toegekend bij definitief maken, bijv. "2026-0001".</summary>
    public string? Factuurnummer { get; set; }
    public int? Jaar { get; set; }
    public int? Volgnummer { get; set; }

    public int KlantId { get; set; }
    public Klant? Klant { get; set; }

    /// <summary>Momentopnames, zodat een definitieve factuur niet verandert als stamgegevens wijzigen.</summary>
    public PartijGegevens KlantGegevens { get; set; } = new();
    public PartijGegevens LeverancierGegevens { get; set; } = new();

    public DateOnly Factuurdatum { get; set; }
    public DateOnly? Leverdatum { get; set; }
    public DateOnly? LeverperiodeEind { get; set; }
    public int BetalingstermijnDagen { get; set; }
    public DateOnly Vervaldatum => Factuurdatum.AddDays(BetalingstermijnDagen);

    public string Iban { get; set; } = "";
    public bool KorToegepast { get; set; }
    public string Taal { get; set; } = "nl-NL";
    public string Valuta { get; set; } = "EUR";
    public string Opmerking { get; set; } = "";

    public int? BriefpapierId { get; set; }

    /// <summary>Bij een creditfactuur: de factuur die gecrediteerd wordt.</summary>
    public int? OrigineleFactuurId { get; set; }
    public Factuur? OrigineleFactuur { get; set; }

    public DateOnly? BetaaldOp { get; set; }
    public DateTime? DefinitiefOp { get; set; }
    public string? PdfBestand { get; set; }

    /// <summary>Totalen vastgelegd bij definitief maken, voor snelle overzichten.</summary>
    public decimal TotaalExclBtw { get; set; }
    public decimal TotaalBtw { get; set; }
    public decimal TotaalInclBtw { get; set; }

    public List<Factuurregel> Regels { get; set; } = [];
    public List<Herinnering> Herinneringen { get; set; } = [];

    /// <summary>Betaalreferentie is gelijk aan het factuurnummer.</summary>
    public string? Betaalreferentie => Factuurnummer;

    public BetaalStatus BetaalStatusOp(DateOnly vandaag)
    {
        if (Status != FactuurStatus.Definitief || TotaalInclBtw <= 0)
            return BetaalStatus.NietVanToepassing;
        if (BetaaldOp is not null)
            return BetaalStatus.Betaald;
        return vandaag > Vervaldatum ? BetaalStatus.Vervallen : BetaalStatus.Openstaand;
    }
}

public class PartijGegevens
{
    public string Naam { get; set; } = "";
    public string Contactpersoon { get; set; } = "";
    public Adres Adres { get; set; } = new();
    public string KvkNummer { get; set; } = "";
    public string BtwNummer { get; set; } = "";
    public string Email { get; set; } = "";

    public static PartijGegevens Van(Klant klant) => new()
    {
        Naam = klant.Naam,
        Contactpersoon = klant.Soort == KlantSoort.Bedrijf ? klant.Contactpersoon : "",
        Adres = klant.EffectiefFactuuradres.Kopie(),
        KvkNummer = klant.Soort == KlantSoort.Bedrijf ? klant.KvkNummer : "",
        BtwNummer = klant.Soort == KlantSoort.Bedrijf ? klant.BtwNummer : "",
        Email = klant.Email,
    };

    public static PartijGegevens Van(Bedrijfsprofiel profiel) => new()
    {
        Naam = profiel.Bedrijfsnaam,
        Adres = profiel.Adres.Kopie(),
        KvkNummer = profiel.KvkNummer,
        BtwNummer = profiel.BtwNummer,
        Email = profiel.Email,
    };
}

public class Factuurregel
{
    public int Id { get; set; }
    public int FactuurId { get; set; }
    public int Volgorde { get; set; }
    public RegelSoort Soort { get; set; }

    public int? ArtikelId { get; set; }
    public string Artikelnummer { get; set; } = "";
    public string Omschrijving { get; set; } = "";
    public decimal Aantal { get; set; } = 1;
    public string Eenheid { get; set; } = "";
    public decimal PrijsExclBtw { get; set; }

    public int BtwGroepId { get; set; }
    public BtwGroep? BtwGroep { get; set; }

    /// <summary>Percentage op factuurdatum; vastgelegd zodat latere tariefwijzigingen geen invloed hebben.</summary>
    public decimal BtwPercentage { get; set; }
}

public class Herinnering
{
    public int Id { get; set; }
    public int FactuurId { get; set; }

    /// <summary>1 = eerste herinnering, 2 = tweede herinnering/aanmaning.</summary>
    public int Niveau { get; set; }
    public DateOnly Datum { get; set; }
    public int BetalingstermijnDagen { get; set; }
    public string? PdfBestand { get; set; }
}
