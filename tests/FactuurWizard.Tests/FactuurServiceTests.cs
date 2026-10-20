using FactuurWizard.Core.Model;
using FactuurWizard.Data;
using FactuurWizard.Data.Services;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace FactuurWizard.Tests;

public sealed class FactuurServiceTests : IDisposable
{
    private readonly SqliteConnection _verbinding = new("Data Source=:memory:");
    private readonly FactuurWizardDbContext _db;

    public FactuurServiceTests()
    {
        _verbinding.Open();
        _db = new FactuurWizardDbContext(new DbContextOptionsBuilder<FactuurWizardDbContext>()
            .UseSqlite(_verbinding).Options);
        DatabaseInitialisatie.Initialiseer(_db);

        var profiel = _db.Bedrijfsprofielen.Single();
        profiel.Bedrijfsnaam = "Test BV";
        profiel.NummeringEersteJaar = 2026;
        profiel.NummeringStartnummer = 38;
        _db.Klanten.Add(new Klant { Klantnummer = 1, Naam = "Jansen", Soort = KlantSoort.Particulier });
        _db.SaveChanges();
    }

    public void Dispose()
    {
        _db.Dispose();
        _verbinding.Dispose();
    }

    private int GroepId(BtwSoort soort) => _db.BtwGroepen.Single(g => g.Soort == soort).Id;

    private Factuur NieuwConcept(DateOnly datum)
    {
        var factuur = new Factuur
        {
            KlantId = _db.Klanten.Single().Id,
            Factuurdatum = datum,
            BetalingstermijnDagen = 14,
            Regels =
            [
                new() { Volgorde = 1, Omschrijving = "Advies (3 uur)", Aantal = 1, PrijsExclBtw = 225m, BtwGroepId = GroepId(BtwSoort.Hoog) },
                new() { Volgorde = 2, Omschrijving = "Boek", Aantal = 2, PrijsExclBtw = 20m, BtwGroepId = GroepId(BtwSoort.Laag) },
            ],
        };
        _db.Facturen.Add(factuur);
        _db.SaveChanges();
        return factuur;
    }

    [Fact]
    public async Task Definitief_maken_kent_nummer_tarieven_en_totalen_toe()
    {
        var service = new FactuurService(_db);

        var f1 = await service.MaakDefinitiefAsync(NieuwConcept(new(2026, 3, 1)).Id, DateTime.Now);
        var f2 = await service.MaakDefinitiefAsync(NieuwConcept(new(2026, 3, 2)).Id, DateTime.Now);
        var f3 = await service.MaakDefinitiefAsync(NieuwConcept(new(2027, 1, 5)).Id, DateTime.Now);

        Assert.Equal("2026-0038", f1.Factuurnummer);
        Assert.Equal("2026-0039", f2.Factuurnummer);
        Assert.Equal("2027-0001", f3.Factuurnummer);
        Assert.Equal("2026-0038", f1.Betaalreferentie);

        Assert.Equal(265m, f1.TotaalExclBtw);
        Assert.Equal(47.25m + 3.60m, f1.TotaalBtw);
        Assert.Equal("Jansen", f1.KlantGegevens.Naam);
        Assert.Equal("Test BV", f1.LeverancierGegevens.Naam);
        Assert.Equal(new DateOnly(2026, 3, 15), f1.Vervaldatum);
    }

    [Fact]
    public async Task Definitieve_factuur_kan_niet_opnieuw_definitief_worden()
    {
        var service = new FactuurService(_db);
        var factuur = await service.MaakDefinitiefAsync(NieuwConcept(new(2026, 3, 1)).Id, DateTime.Now);

        await Assert.ThrowsAsync<FactuurException>(() => service.MaakDefinitiefAsync(factuur.Id, DateTime.Now));
    }

    [Fact]
    public async Task Kor_geeft_factuur_zonder_btw()
    {
        _db.Bedrijfsprofielen.Single().KorIngangsdatum = new(2026, 1, 1);
        _db.SaveChanges();

        var factuur = await new FactuurService(_db).MaakDefinitiefAsync(NieuwConcept(new(2026, 3, 1)).Id, DateTime.Now);

        Assert.True(factuur.KorToegepast);
        Assert.Equal(0m, factuur.TotaalBtw);
        Assert.Equal(265m, factuur.TotaalInclBtw);
    }

    [Fact]
    public async Task Creditfactuur_heft_factuur_op_en_volgt_dezelfde_reeks()
    {
        var service = new FactuurService(_db);
        var factuur = await service.MaakDefinitiefAsync(NieuwConcept(new(2026, 3, 1)).Id, DateTime.Now);

        var credit = await service.MaakCreditfactuurAsync(factuur.Id, new(2026, 4, 1));
        credit = await service.MaakDefinitiefAsync(credit.Id, DateTime.Now);

        Assert.Equal(FactuurSoort.Creditfactuur, credit.Soort);
        Assert.Equal("2026-0039", credit.Factuurnummer);
        Assert.Equal(-factuur.TotaalInclBtw, credit.TotaalInclBtw);
    }

    [Fact]
    public async Task Volledige_creditering_maakt_factuur_gecrediteerd_en_gedeeltelijke_verlaagt_openstaand()
    {
        var service = new FactuurService(_db);
        var factuur = await service.MaakDefinitiefAsync(NieuwConcept(new(2026, 3, 1)).Id, DateTime.Now);

        // Gedeeltelijk: alleen de eerste regel crediteren.
        var deel = await service.MaakCreditfactuurAsync(factuur.Id, new(2026, 3, 5));
        deel.Regels.RemoveAll(r => r.Volgorde == 2);
        await _db.SaveChangesAsync();
        await service.MaakDefinitiefAsync(deel.Id, DateTime.Now);

        var gecrediteerd = await service.GecrediteerdAsync(factuur.Id);
        Assert.Equal(-272.25m, gecrediteerd);
        Assert.Equal(43.60m, factuur.OpenstaandBedrag(gecrediteerd));
        Assert.Equal(BetaalStatus.Openstaand, factuur.BetaalStatusOp(new(2026, 3, 6), gecrediteerd));

        // Rest crediteren.
        var rest = await service.MaakCreditfactuurAsync(factuur.Id, new(2026, 3, 6));
        rest.Regels.RemoveAll(r => r.Volgorde == 1);
        await _db.SaveChangesAsync();
        await service.MaakDefinitiefAsync(rest.Id, DateTime.Now);

        Assert.Equal(BetaalStatus.Gecrediteerd, factuur.BetaalStatusOp(new(2026, 4, 1), await service.GecrediteerdAsync(factuur.Id)));
    }

    [Fact]
    public async Task Herinneringen_lopen_op_tot_niveau_2_en_niet_na_betaling()
    {
        var service = new FactuurService(_db);
        var factuur = await service.MaakDefinitiefAsync(NieuwConcept(new(2026, 3, 1)).Id, DateTime.Now);

        Assert.Equal(1, (await service.MaakHerinneringAsync(factuur.Id, new(2026, 3, 20), 7)).Niveau);
        Assert.Equal(2, (await service.MaakHerinneringAsync(factuur.Id, new(2026, 3, 30), 7)).Niveau);
        Assert.Equal(2, (await service.MaakHerinneringAsync(factuur.Id, new(2026, 4, 10), 7)).Niveau);

        await service.ZetBetaaldAsync(factuur.Id, new(2026, 4, 12));
        await Assert.ThrowsAsync<FactuurException>(() => service.MaakHerinneringAsync(factuur.Id, new(2026, 4, 20), 7));
    }

    [Fact]
    public async Task Kopie_is_nieuw_concept_met_dezelfde_regels()
    {
        var service = new FactuurService(_db);
        var factuur = await service.MaakDefinitiefAsync(NieuwConcept(new(2026, 3, 1)).Id, DateTime.Now);

        var kopie = await service.MaakKopieAsync(factuur.Id, new(2026, 5, 1), 30);

        Assert.Equal(FactuurStatus.Concept, kopie.Status);
        Assert.Null(kopie.Factuurnummer);
        Assert.Equal(factuur.KlantId, kopie.KlantId);
        Assert.Equal(factuur.Regels.Select(r => r.Omschrijving), kopie.Regels.Select(r => r.Omschrijving));
    }

    [Fact]
    public async Task Betaalstatus_volgt_vervaldatum_en_betaling()
    {
        var factuur = await new FactuurService(_db).MaakDefinitiefAsync(NieuwConcept(new(2026, 3, 1)).Id, DateTime.Now);

        Assert.Equal(BetaalStatus.Openstaand, factuur.BetaalStatusOp(new(2026, 3, 15)));
        Assert.Equal(BetaalStatus.Vervallen, factuur.BetaalStatusOp(new(2026, 3, 16)));
        factuur.BetaaldOp = new(2026, 3, 20);
        Assert.Equal(BetaalStatus.Betaald, factuur.BetaalStatusOp(new(2026, 4, 1)));
    }
}
