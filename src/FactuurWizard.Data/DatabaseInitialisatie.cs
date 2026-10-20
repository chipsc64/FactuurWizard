using FactuurWizard.Core.Model;
using Microsoft.EntityFrameworkCore;

namespace FactuurWizard.Data;

public static class DatabaseInitialisatie
{
    /// <summary>
    /// Voert openstaande migraties uit en vult een lege database met standaardgegevens.
    /// </summary>
    public static void Initialiseer(FactuurWizardDbContext db)
    {
        db.Database.Migrate();
        VulStandaardgegevens(db);
    }

    public static void VulStandaardgegevens(FactuurWizardDbContext db)
    {
        if (!db.Bedrijfsprofielen.Any())
            db.Bedrijfsprofielen.Add(new Bedrijfsprofiel());

        if (!db.BtwGroepen.Any())
        {
            db.BtwGroepen.AddRange(
                new BtwGroep
                {
                    Naam = "Hoog", Soort = BtwSoort.Hoog,
                    Tarieven =
                    [
                        new() { Percentage = 19m, Ingangsdatum = new(2001, 1, 1), Einddatum = new(2012, 9, 30) },
                        new() { Percentage = 21m, Ingangsdatum = new(2012, 10, 1) },
                    ],
                },
                new BtwGroep
                {
                    Naam = "Laag", Soort = BtwSoort.Laag,
                    Tarieven =
                    [
                        new() { Percentage = 6m, Ingangsdatum = new(2001, 1, 1), Einddatum = new(2018, 12, 31) },
                        new() { Percentage = 9m, Ingangsdatum = new(2019, 1, 1) },
                    ],
                },
                new BtwGroep
                {
                    Naam = "Nul", Soort = BtwSoort.Nul,
                    Tarieven = [new() { Percentage = 0m, Ingangsdatum = new(2001, 1, 1) }],
                });
        }

        if (!db.Briefpapieren.Any())
            db.Briefpapieren.Add(new Briefpapier { IsStandaard = true });

        if (!db.Tekstsjablonen.Any())
            db.Tekstsjablonen.AddRange(StandaardTeksten.Select(t => new Tekstsjabloon { Soort = t.Key, Tekst = t.Value }));

        db.SaveChanges();
    }

    private static readonly Dictionary<TekstSoort, string> StandaardTeksten = new()
    {
        [TekstSoort.Betalingsvoorwaarden] =
            "Wij verzoeken u het totaalbedrag vóór {Vervaldatum} over te maken op {Iban} t.n.v. {Bedrijfsnaam}, " +
            "onder vermelding van {Betaalreferentie}.",
        [TekstSoort.AlgemeneVoorwaarden] = "",
        [TekstSoort.Afsluittekst] = "Hartelijk dank voor uw opdracht.",
        [TekstSoort.EmailOnderwerp] = "Factuur {Factuurnummer} van {Bedrijfsnaam}",
        [TekstSoort.EmailTekst] =
            "Beste {Klantnaam},\n\nIn de bijlage vindt u factuur {Factuurnummer} van {Factuurdatum} " +
            "ter hoogte van {TotaalInclBtw}.\n\nMet vriendelijke groet,\n{Bedrijfsnaam}",
        [TekstSoort.Herinnering1Tekst] =
            "Volgens onze administratie is factuur {Factuurnummer} van {Factuurdatum} ter hoogte van {TotaalInclBtw} " +
            "nog niet betaald. Wij verzoeken u het bedrag binnen {Termijn} dagen over te maken op {Iban} " +
            "onder vermelding van {Betaalreferentie}. Heeft u inmiddels betaald, dan kunt u deze herinnering als " +
            "niet verzonden beschouwen.",
        [TekstSoort.Herinnering2Tekst] =
            "Ondanks onze eerdere herinnering hebben wij de betaling van factuur {Factuurnummer} van {Factuurdatum} " +
            "ter hoogte van {TotaalInclBtw} nog niet ontvangen. Wij verzoeken u dringend het bedrag binnen {Termijn} " +
            "dagen over te maken op {Iban} onder vermelding van {Betaalreferentie}.",
        [TekstSoort.HerinneringEmailOnderwerp] = "Betalingsherinnering factuur {Factuurnummer}",
        [TekstSoort.HerinneringEmailTekst] =
            "Beste {Klantnaam},\n\nIn de bijlage vindt u een betalingsherinnering voor factuur {Factuurnummer}." +
            "\n\nMet vriendelijke groet,\n{Bedrijfsnaam}",
    };
}
