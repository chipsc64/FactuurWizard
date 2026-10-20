namespace FactuurWizard.Core.Model;

/// <summary>
/// Gegevens en instellingen van de facturerende partij. Er is precies één profiel.
/// </summary>
public class Bedrijfsprofiel
{
    public int Id { get; set; }

    public string Bedrijfsnaam { get; set; } = "";
    public Adres Adres { get; set; } = new();
    public string KvkNummer { get; set; } = "";
    public string BtwNummer { get; set; } = "";
    public string Iban { get; set; } = "";
    public string Bic { get; set; } = "";
    public string TenaamstellingRekening { get; set; } = "";
    public string Email { get; set; } = "";
    public string Telefoon { get; set; } = "";
    public string Website { get; set; } = "";

    public int StandaardBetalingstermijnDagen { get; set; } = 14;

    /// <summary>Jaar waarvoor <see cref="NummeringStartnummer"/> geldt; latere jaren beginnen bij 1.</summary>
    public int NummeringEersteJaar { get; set; } = DateTime.Today.Year;
    public int NummeringStartnummer { get; set; } = 1;

    /// <summary>Kleineondernemersregeling: van toepassing op facturen met een factuurdatum binnen deze periode.</summary>
    public DateOnly? KorIngangsdatum { get; set; }
    public DateOnly? KorEinddatum { get; set; }

    public string Taal { get; set; } = "nl-NL";
    public string Valuta { get; set; } = "EUR";

    public bool KorVanToepassing(DateOnly datum) =>
        KorIngangsdatum is { } start && datum >= start && (KorEinddatum is not { } eind || datum <= eind);
}
