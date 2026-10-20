using FactuurWizard.Core.Model;

namespace FactuurWizard.Core.Services;

public static class KlantValidatie
{
    public static void Normaliseer(Klant k)
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

    public static IEnumerable<string> Valideer(Klant k)
    {
        if (k.Naam.Trim().Length == 0)
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
