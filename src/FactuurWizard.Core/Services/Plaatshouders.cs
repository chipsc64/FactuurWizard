using System.Text.RegularExpressions;

namespace FactuurWizard.Core.Services;

/// <summary>
/// Vult plaatshouders als {Factuurnummer} in vaste teksten. Onbekende plaatshouders blijven staan.
/// </summary>
public static partial class Plaatshouders
{
    public static readonly IReadOnlyList<(string Naam, string Uitleg)> Beschikbaar =
    [
        ("Factuurnummer", "Nummer van de factuur, bijv. 2026-0001"),
        ("Factuurdatum", "Datum van de factuur"),
        ("Vervaldatum", "Uiterste betaaldatum"),
        ("TotaalInclBtw", "Te betalen bedrag"),
        ("Betaalreferentie", "Referentie voor de betaling (= factuurnummer)"),
        ("Klantnaam", "Naam van de klant of contactpersoon"),
        ("Bedrijfsnaam", "Uw bedrijfsnaam"),
        ("Iban", "Uw IBAN"),
        ("Termijn", "Betalingstermijn in dagen (herinneringen)"),
    ];

    private static readonly System.Globalization.CultureInfo Nl = new("nl-NL");

    /// <summary>Waarden voor een (definitieve of concept)factuur.</summary>
    public static Dictionary<string, string> VoorFactuur(Model.Factuur factuur, Model.Bedrijfsprofiel profiel, int? termijn = null)
    {
        var klant = factuur.Status == Model.FactuurStatus.Definitief ? factuur.KlantGegevens
            : factuur.Klant is { } k ? Model.PartijGegevens.Van(k) : new Model.PartijGegevens();
        var nummer = factuur.Factuurnummer ?? "(concept)";
        return new Dictionary<string, string>
        {
            ["Factuurnummer"] = nummer,
            ["Factuurdatum"] = factuur.Factuurdatum.ToString("d-M-yyyy", Nl),
            ["Vervaldatum"] = factuur.Vervaldatum.ToString("d-M-yyyy", Nl),
            ["TotaalInclBtw"] = factuur.TotaalInclBtw.ToString("C", Nl),
            ["Betaalreferentie"] = nummer,
            ["Klantnaam"] = klant.Contactpersoon.Length > 0 ? klant.Contactpersoon : klant.Naam,
            ["Bedrijfsnaam"] = profiel.Bedrijfsnaam,
            ["Iban"] = factuur.Iban.Length > 0 ? factuur.Iban : profiel.Iban,
            ["Termijn"] = (termijn ?? factuur.BetalingstermijnDagen).ToString(),
        };
    }

    public static string Vul(string tekst, IReadOnlyDictionary<string, string> waarden) =>
        PlaatshouderRegex().Replace(tekst, m => waarden.TryGetValue(m.Groups[1].Value, out var w) ? w : m.Value);

    [GeneratedRegex(@"\{(\w+)\}")]
    private static partial Regex PlaatshouderRegex();
}
