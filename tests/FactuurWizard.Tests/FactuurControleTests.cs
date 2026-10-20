using FactuurWizard.Core.Model;
using FactuurWizard.Core.Services;

namespace FactuurWizard.Tests;

public class FactuurControleTests
{
    private static readonly DateOnly Vandaag = new(2026, 3, 1);

    private static Adres VolledigAdres() => new() { Straat = "Dorpsstraat", Huisnummer = "1", Postcode = "1234 AB", Plaats = "Ergens" };

    private static Bedrijfsprofiel Profiel() => new()
    {
        Bedrijfsnaam = "Test BV", Adres = VolledigAdres(), KvkNummer = "12345678",
        BtwNummer = "NL123456789B01", Iban = "NL91 ABNA 0417 1643 00",
    };

    private static Klant Klant() => new() { Naam = "Jansen", Adres = VolledigAdres() };

    private static Factuur Factuur(params Factuurregel[] regels) => new() { Factuurdatum = Vandaag, Regels = [.. regels] };

    private static Factuurregel Regel(string omschrijving = "Werk", decimal aantal = 1) =>
        new() { Omschrijving = omschrijving, Aantal = aantal, PrijsExclBtw = 100m, BtwPercentage = 21m };

    [Fact]
    public void Volledige_factuur_is_goed()
    {
        var r = FactuurControle.VoorDefinitief(Factuur(Regel()), Klant(), Profiel(), null, Vandaag);
        Assert.True(r.IsGoed);
        Assert.Empty(r.Waarschuwingen);
    }

    [Fact]
    public void Ontbrekende_wettelijke_gegevens_zijn_fouten()
    {
        var profiel = Profiel();
        profiel.KvkNummer = "";
        profiel.BtwNummer = "";
        var klant = Klant();
        klant.Adres = new Adres();

        var r = FactuurControle.VoorDefinitief(Factuur(Regel(omschrijving: ""), Regel(aantal: 0)), klant, profiel, null, Vandaag);

        Assert.Equal(5, r.Fouten.Count);
    }

    [Fact]
    public void Afwijkend_factuuradres_telt_als_adres()
    {
        var klant = Klant();
        klant.Adres = new Adres();
        klant.Factuuradres = VolledigAdres();

        Assert.True(FactuurControle.VoorDefinitief(Factuur(Regel()), klant, Profiel(), null, Vandaag).IsGoed);
    }

    [Fact]
    public void Datum_voor_vorige_factuur_geeft_waarschuwing()
    {
        var r = FactuurControle.VoorDefinitief(Factuur(Regel()), Klant(), Profiel(), Vandaag.AddDays(1), Vandaag);
        Assert.True(r.IsGoed);
        Assert.Single(r.Waarschuwingen);
    }
}
