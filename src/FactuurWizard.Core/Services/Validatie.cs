using System.Numerics;
using System.Text.RegularExpressions;

namespace FactuurWizard.Core.Services;

public static partial class Validatie
{
    public static string Compact(string waarde) => WitruimteRegex().Replace(waarde, "").ToUpperInvariant();

    public static bool IsGeldigeIban(string iban)
    {
        var compact = Compact(iban);
        if (!IbanRegex().IsMatch(compact))
            return false;

        // ISO 13616: eerste vier tekens achteraan, letters omzetten naar getallen (A=10), dan modulo 97 == 1.
        var herschikt = compact[4..] + compact[..4];
        var cijfers = string.Concat(herschikt.Select(c => char.IsLetter(c) ? (c - 'A' + 10).ToString() : c.ToString()));
        return BigInteger.Parse(cijfers) % 97 == 1;
    }

    /// <summary>Formatteert een IBAN in groepen van vier, bijv. "NL91 ABNA 0417 1643 00".</summary>
    public static string FormatteerIban(string iban)
    {
        var compact = Compact(iban);
        return string.Join(' ', compact.Chunk(4).Select(c => new string(c)));
    }

    /// <summary>
    /// Nederlandse btw-nummers moeten het formaat NL123456789B01 hebben; voor andere EU-landen
    /// wordt alleen een globale controle gedaan.
    /// </summary>
    public static bool IsGeldigBtwNummer(string btwNummer)
    {
        var compact = Compact(btwNummer).Replace(".", "");
        return compact.StartsWith("NL") ? NlBtwRegex().IsMatch(compact) : EuBtwRegex().IsMatch(compact);
    }

    public static bool IsGeldigKvkNummer(string kvk) => KvkRegex().IsMatch(Compact(kvk));

    public static bool IsGeldigePostcode(string postcode) => PostcodeRegex().IsMatch(postcode.Trim());

    public static string FormatteerPostcode(string postcode)
    {
        var compact = Compact(postcode);
        return compact.Length == 6 ? $"{compact[..4]} {compact[4..]}" : postcode.Trim();
    }

    public static bool IsGeldigEmailadres(string email) => EmailRegex().IsMatch(email.Trim());

    public static bool IsGeldigeKleur(string kleur) => KleurRegex().IsMatch(kleur.Trim());

    [GeneratedRegex(@"\s+")]
    private static partial Regex WitruimteRegex();

    [GeneratedRegex(@"^[A-Z]{2}\d{2}[A-Z0-9]{11,30}$")]
    private static partial Regex IbanRegex();

    [GeneratedRegex(@"^NL\d{9}B\d{2}$")]
    private static partial Regex NlBtwRegex();

    [GeneratedRegex(@"^[A-Z]{2}[A-Z0-9+*]{2,12}$")]
    private static partial Regex EuBtwRegex();

    [GeneratedRegex(@"^\d{8}$")]
    private static partial Regex KvkRegex();

    [GeneratedRegex(@"^\d{4}\s?[A-Za-z]{2}$")]
    private static partial Regex PostcodeRegex();

    [GeneratedRegex(@"^[^@\s]+@[^@\s]+\.[^@\s]+$")]
    private static partial Regex EmailRegex();

    [GeneratedRegex(@"^#[0-9A-Fa-f]{6}$")]
    private static partial Regex KleurRegex();
}
