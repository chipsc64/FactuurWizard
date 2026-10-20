using FactuurWizard.Core.Model;
using FactuurWizard.Core.Services;

namespace FactuurWizard.Tests;

public class BtwRapportageTests
{
    private const int Hoog = 1, Laag = 2, Nul = 3;

    private static readonly Dictionary<int, BtwSoort> Soorten = new()
    {
        [Hoog] = BtwSoort.Hoog, [Laag] = BtwSoort.Laag, [Nul] = BtwSoort.Nul,
    };

    private static Factuur Definitief(int volgnummer, DateOnly datum, params (int Groep, decimal Pct, decimal Aantal, decimal Prijs)[] regels) => new()
    {
        Status = FactuurStatus.Definitief,
        Jaar = datum.Year,
        Volgnummer = volgnummer,
        Factuurnummer = Factuurnummering.Formatteer(datum.Year, volgnummer),
        Factuurdatum = datum,
        Regels = regels.Select((r, i) => new Factuurregel
        {
            Volgorde = i + 1, BtwGroepId = r.Groep, BtwPercentage = r.Pct, Aantal = r.Aantal, PrijsExclBtw = r.Prijs,
        }).ToList(),
    };

    [Theory]
    [InlineData(1, "2026-01-01", "2026-03-31")]
    [InlineData(4, "2026-10-01", "2026-12-31")]
    [InlineData(null, "2026-01-01", "2026-12-31")]
    public void Periode_per_kwartaal_of_jaar(int? kwartaal, string van, string tot)
    {
        Assert.Equal((DateOnly.Parse(van), DateOnly.Parse(tot)), BtwRapportage.Periode(2026, kwartaal));
    }

    [Fact]
    public void Facturen_worden_per_rubriek_opgeteld_en_creditfacturen_verrekend()
    {
        var facturen = new List<Factuur>
        {
            Definitief(1, new(2026, 7, 1), (Hoog, 21m, 1, 225m), (Laag, 9m, 2, 20m), (Nul, 0m, 1, 50m)),
            Definitief(2, new(2026, 8, 15), (Hoog, 21m, 1, 100m)),
            Definitief(3, new(2026, 9, 30), (Hoog, 21m, -1, 100m)),   // creditfactuur
            Definitief(4, new(2026, 10, 1), (Hoog, 21m, 1, 999m)),    // valt buiten Q3
            new() { Status = FactuurStatus.Concept, Factuurdatum = new(2026, 7, 2), Regels = [new() { BtwGroepId = Hoog, BtwPercentage = 21, Aantal = 1, PrijsExclBtw = 500 }] },
        };
        var (van, tot) = BtwRapportage.Periode(2026, 3);

        var overzicht = BtwRapportage.Bereken(facturen, Soorten, van, tot);

        Assert.Equal(3, overzicht.Facturen.Count);
        Assert.Equal(new RubriekRegel("1a", BtwRapportage.Omschrijving1a, 225m, 47.25m), overzicht.Rubrieken[0]);
        Assert.Equal(new RubriekRegel("1b", BtwRapportage.Omschrijving1b, 40m, 3.60m), overzicht.Rubrieken[1]);
        Assert.Equal(new RubriekRegel("1e", BtwRapportage.Omschrijving1e, 50m, 0m), overzicht.Rubrieken[2]);
        Assert.Equal(50.85m, overzicht.VerschuldigdeBtw);
        // De specificatie sluit aan op de totalen van de facturen zelf.
        Assert.Equal(overzicht.VerschuldigdeBtw, overzicht.Facturen.Sum(f => f.TotaalBtw));
    }

    [Fact]
    public void Kor_facturen_tellen_niet_mee_in_de_rubrieken()
    {
        var kor = Definitief(1, new(2026, 2, 1), (Hoog, 0m, 1, 300m));
        kor.KorToegepast = true;

        var overzicht = BtwRapportage.Bereken([kor], Soorten, new(2026, 1, 1), new(2026, 3, 31));

        Assert.All(overzicht.Rubrieken, r => Assert.Equal(0m, r.Omzet));
        Assert.Equal(300m, overzicht.OmzetKor);
    }

    [Fact]
    public void Hele_euros_naar_beneden() => Assert.Equal(47m, BtwRapportage.HeleEuros(47.99m));

    [Fact]
    public void Excel_export_bevat_rubrieken_en_specificatie()
    {
        var factuur = Definitief(1, new(2026, 7, 1), (Hoog, 21m, 1, 225m), (Laag, 9m, 2, 20m));
        factuur.KlantGegevens.Naam = "Jansen";
        var overzicht = BtwRapportage.Bereken([factuur], Soorten, new(2026, 7, 1), new(2026, 9, 30));

        using var stroom = new MemoryStream();
        FactuurWizard.Export.BtwExcel.Exporteer(overzicht, "Test BV", "2026 kwartaal 3", stroom);

        stroom.Position = 0;
        using var werkmap = new ClosedXML.Excel.XLWorkbook(stroom);
        var blad = werkmap.Worksheet("Overzicht");
        Assert.Equal("1a", blad.Cell(6, 1).GetString());
        Assert.Equal(225m, blad.Cell(6, 3).GetValue<decimal>());
        Assert.Equal(50.85m, blad.Cell(9, 4).GetValue<decimal>());
        var spec = werkmap.Worksheet("Specificatie");
        Assert.Equal("2026-0001", spec.Cell(2, 1).GetString());
        Assert.Equal("Jansen", spec.Cell(2, 4).GetString());
        Assert.Equal(50.85m, spec.Cell(3, 13).GetValue<decimal>());
    }
}
