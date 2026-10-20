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

    public static string Vul(string tekst, IReadOnlyDictionary<string, string> waarden) =>
        PlaatshouderRegex().Replace(tekst, m => waarden.TryGetValue(m.Groups[1].Value, out var w) ? w : m.Value);

    [GeneratedRegex(@"\{(\w+)\}")]
    private static partial Regex PlaatshouderRegex();
}
