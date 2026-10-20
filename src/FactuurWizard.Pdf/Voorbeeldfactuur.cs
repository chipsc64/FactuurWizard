using FactuurWizard.Core.Model;

namespace FactuurWizard.Pdf;

/// <summary>
/// Fictieve factuur om het briefpapier te beoordelen, met de echte bedrijfsgegevens.
/// </summary>
public static class Voorbeeldfactuur
{
    public static FactuurDocumentInvoer Maak(
        Bedrijfsprofiel profiel, Briefpapier briefpapier, IReadOnlyDictionary<TekstSoort, string> teksten)
    {
        var vandaag = DateOnly.FromDateTime(DateTime.Today);
        var kor = profiel.KorVanToepassing(vandaag);
        var factuur = new Factuur
        {
            Status = FactuurStatus.Definitief,
            Factuurnummer = $"{vandaag.Year}-0001",
            Factuurdatum = vandaag,
            Leverdatum = vandaag.AddDays(-7),
            BetalingstermijnDagen = profiel.StandaardBetalingstermijnDagen,
            LeverancierGegevens = PartijGegevens.Van(profiel),
            KlantGegevens = new PartijGegevens
            {
                Naam = "Voorbeeld Klant BV",
                Contactpersoon = "J. Jansen",
                Adres = new Adres { Straat = "Voorbeeldstraat", Huisnummer = "12", Postcode = "1234 AB", Plaats = "Voorbeeldstad" },
            },
            Iban = profiel.Iban,
            KorToegepast = kor,
            Opmerking = "Dit is een voorbeeld van uw factuur.",
            Regels =
            [
                new() { Volgorde = 1, Omschrijving = "Advieswerkzaamheden (3 uur)", Aantal = 1, Eenheid = "", PrijsExclBtw = 225m, BtwPercentage = kor ? 0 : 21m, BtwGroepId = 1 },
                new() { Volgorde = 2, Artikelnummer = "1001", Omschrijving = "Handboek", Aantal = 2, Eenheid = "stuk", PrijsExclBtw = 19.95m, BtwPercentage = kor ? 0 : 9m, BtwGroepId = 2 },
            ],
        };
        return new FactuurDocumentInvoer(factuur, profiel, briefpapier, teksten, Klantnummer: 1001, Watermerk: "VOORBEELD");
    }
}
