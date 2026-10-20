namespace FactuurWizard.Core.Model;

public class Klant
{
    public int Id { get; set; }
    public int Klantnummer { get; set; }
    public KlantSoort Soort { get; set; }

    /// <summary>Bedrijfsnaam of, bij een particulier, de volledige naam.</summary>
    public string Naam { get; set; } = "";
    public string Contactpersoon { get; set; } = "";
    public Adres Adres { get; set; } = new();

    /// <summary>Optioneel afwijkend factuuradres; leeg betekent gebruik <see cref="Adres"/>.</summary>
    public Adres? Factuuradres { get; set; }

    public string Email { get; set; } = "";
    public string Telefoon { get; set; } = "";
    public string KvkNummer { get; set; } = "";
    public string BtwNummer { get; set; } = "";

    /// <summary>Afwijkende betalingstermijn; leeg betekent de standaard uit het bedrijfsprofiel.</summary>
    public int? BetalingstermijnDagen { get; set; }

    public string Notities { get; set; } = "";

    /// <summary>Klanten met facturen worden niet verwijderd maar gearchiveerd.</summary>
    public bool Gearchiveerd { get; set; }

    public Adres EffectiefFactuuradres => Factuuradres is { IsLeeg: false } ? Factuuradres : Adres;
}
