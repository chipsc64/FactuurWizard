using FactuurWizard.Core.Model;
using FactuurWizard.Pdf;

namespace FactuurWizard.Tests;

public class FactuurPdfTests
{
    private static Bedrijfsprofiel Profiel() => new()
    {
        Bedrijfsnaam = "Test BV",
        Adres = new Adres { Straat = "Hoofdweg", Huisnummer = "10", Postcode = "3511 AB", Plaats = "Utrecht" },
        KvkNummer = "12345678", BtwNummer = "NL123456789B01", Iban = "NL91 ABNA 0417 1643 00",
    };

    private static readonly Dictionary<TekstSoort, string> Teksten = new()
    {
        [TekstSoort.Betalingsvoorwaarden] = "Betaal vóór {Vervaldatum} onder vermelding van {Betaalreferentie}.",
        [TekstSoort.Afsluittekst] = "Bedankt!",
    };

    [Fact]
    public void Voorbeeldfactuur_geeft_een_pdf_van_een_pagina()
    {
        var invoer = Voorbeeldfactuur.Maak(Profiel(), new Briefpapier(), Teksten);

        var pdf = FactuurPdf.Genereer(invoer);

        Assert.Equal("%PDF"u8.ToArray(), pdf[..4]);
        Assert.Single(FactuurPdf.GenereerAfbeeldingen(invoer));
    }

    [Fact]
    public void Veel_regels_lopen_door_op_volgende_paginas()
    {
        var invoer = Voorbeeldfactuur.Maak(Profiel(), new Briefpapier(), Teksten);
        invoer.Factuur.Regels = Enumerable.Range(1, 80)
            .Select(i => new Factuurregel { Volgorde = i, Omschrijving = $"Regel {i}", Aantal = 1, PrijsExclBtw = 10m, BtwPercentage = 21m })
            .ToList();

        Assert.True(FactuurPdf.GenereerAfbeeldingen(invoer).Count > 1);
    }

    [Fact]
    public void Afbeelding_als_briefpapier_en_logo_worden_verwerkt()
    {
        var png = FactuurPdf.GenereerAfbeeldingen(Voorbeeldfactuur.Maak(Profiel(), new Briefpapier(), Teksten))[0];
        var briefpapier = new Briefpapier { AchtergrondSoort = AchtergrondSoort.Afbeelding, Achtergrond = png, Logo = png };

        var pdf = FactuurPdf.Genereer(Voorbeeldfactuur.Maak(Profiel(), briefpapier, Teksten));

        Assert.True(pdf.Length > png.Length);
    }

    [Fact]
    public void Ontbrekend_lettertype_valt_terug_op_reserve()
    {
        var briefpapier = new Briefpapier { Lettertype = "Bestaat Niet 123" };
        var pdf = FactuurPdf.Genereer(Voorbeeldfactuur.Maak(Profiel(), briefpapier, Teksten));
        Assert.Equal("%PDF"u8.ToArray(), pdf[..4]);
    }

    /// <summary>Schrijft een voorbeeld naar de tijdelijke map, om de opmaak te kunnen bekijken.</summary>
    [Fact]
    public void Voorbeeld_naar_tijdelijke_map()
    {
        var map = Path.Combine(Path.GetTempPath(), "FactuurWizardTest");
        Directory.CreateDirectory(map);
        var invoer = Voorbeeldfactuur.Maak(Profiel(), new Briefpapier(), Teksten);
        File.WriteAllBytes(Path.Combine(map, "voorbeeld.pdf"), FactuurPdf.Genereer(invoer));
        File.WriteAllBytes(Path.Combine(map, "voorbeeld.png"), FactuurPdf.GenereerAfbeeldingen(invoer, 110)[0]);
    }
}
