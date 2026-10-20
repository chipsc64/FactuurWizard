using FactuurWizard.Core.Model;
using FactuurWizard.Core.Services;
using Microsoft.EntityFrameworkCore;

namespace FactuurWizard.Data.Services;

public class FactuurException(string message) : Exception(message);

public class FactuurService(FactuurWizardDbContext db)
{
    /// <summary>
    /// Maakt een conceptfactuur definitief: kent het factuurnummer toe, legt btw-percentages op factuurdatum,
    /// klant- en leveranciersgegevens en totalen vast. Daarna is de factuur niet meer te wijzigen.
    /// </summary>
    public async Task<Factuur> MaakDefinitiefAsync(int factuurId, DateTime nu)
    {
        var factuur = await db.Facturen
            .Include(f => f.Regels)
            .Include(f => f.Klant)
            .SingleOrDefaultAsync(f => f.Id == factuurId)
            ?? throw new FactuurException($"Factuur {factuurId} bestaat niet.");

        if (factuur.Status != FactuurStatus.Concept)
            throw new FactuurException("Alleen een conceptfactuur kan definitief gemaakt worden.");
        if (factuur.Regels.Count == 0)
            throw new FactuurException("De factuur bevat geen regels.");

        var profiel = await db.Bedrijfsprofielen.SingleAsync();

        await using var transactie = await db.Database.BeginTransactionAsync();

        bool kor;
        if (factuur.Soort == FactuurSoort.Creditfactuur)
        {
            // Een creditfactuur volgt de btw-behandeling van de originele factuur; percentages blijven ongewijzigd.
            var origineel = await db.Facturen.SingleAsync(f => f.Id == factuur.OrigineleFactuurId);
            kor = origineel.KorToegepast;
        }
        else
        {
            var groepen = await db.BtwGroepen.Include(g => g.Tarieven).ToDictionaryAsync(g => g.Id);
            kor = profiel.KorVanToepassing(factuur.Factuurdatum);
            foreach (var regel in factuur.Regels)
                regel.BtwPercentage = kor
                    ? 0m
                    : BtwTariefBepaling.PercentageOp(groepen[regel.BtwGroepId], factuur.Factuurdatum);
        }

        var jaar = factuur.Factuurdatum.Year;
        var reeks = await db.Nummerreeksen.FindAsync(jaar);
        var volgnummer = Factuurnummering.VolgendVolgnummer(
            reeks?.LaatsteVolgnummer, jaar, profiel.NummeringEersteJaar, profiel.NummeringStartnummer);
        if (reeks is null)
            db.Nummerreeksen.Add(reeks = new Nummerreeks { Jaar = jaar });
        reeks.LaatsteVolgnummer = volgnummer;

        var totalen = FactuurBerekening.Bereken(factuur.Regels);

        factuur.Status = FactuurStatus.Definitief;
        factuur.Jaar = jaar;
        factuur.Volgnummer = volgnummer;
        factuur.Factuurnummer = Factuurnummering.Formatteer(jaar, volgnummer);
        factuur.KlantGegevens = PartijGegevens.Van(factuur.Klant!);
        factuur.LeverancierGegevens = PartijGegevens.Van(profiel);
        factuur.Iban = profiel.Iban;
        factuur.KorToegepast = kor;
        factuur.TotaalExclBtw = totalen.TotaalExclBtw;
        factuur.TotaalBtw = totalen.TotaalBtw;
        factuur.TotaalInclBtw = totalen.TotaalInclBtw;
        factuur.DefinitiefOp = nu;

        await db.SaveChangesAsync();
        await transactie.CommitAsync();
        return factuur;
    }

    /// <summary>
    /// Maakt een conceptcreditfactuur met dezelfde regels als de originele factuur, met negatieve aantallen.
    /// </summary>
    public async Task<Factuur> MaakCreditfactuurAsync(int origineleFactuurId, DateOnly factuurdatum)
    {
        var origineel = await db.Facturen
            .Include(f => f.Regels)
            .SingleOrDefaultAsync(f => f.Id == origineleFactuurId)
            ?? throw new FactuurException($"Factuur {origineleFactuurId} bestaat niet.");

        if (origineel.Status != FactuurStatus.Definitief || origineel.Soort != FactuurSoort.Factuur)
            throw new FactuurException("Alleen een definitieve factuur kan gecrediteerd worden.");

        var credit = new Factuur
        {
            Soort = FactuurSoort.Creditfactuur,
            Status = FactuurStatus.Concept,
            KlantId = origineel.KlantId,
            Factuurdatum = factuurdatum,
            Leverdatum = origineel.Leverdatum,
            LeverperiodeEind = origineel.LeverperiodeEind,
            BetalingstermijnDagen = 0,
            Taal = origineel.Taal,
            Valuta = origineel.Valuta,
            BriefpapierId = origineel.BriefpapierId,
            // De verwijzing naar de originele factuur staat al standaard op de creditfactuur.
            OrigineleFactuurId = origineel.Id,
            Regels = origineel.Regels.OrderBy(r => r.Volgorde).Select(r => new Factuurregel
            {
                Volgorde = r.Volgorde,
                Soort = r.Soort,
                ArtikelId = r.ArtikelId,
                Artikelnummer = r.Artikelnummer,
                Omschrijving = r.Omschrijving,
                Aantal = -r.Aantal,
                Eenheid = r.Eenheid,
                PrijsExclBtw = r.PrijsExclBtw,
                BtwGroepId = r.BtwGroepId,
                BtwPercentage = r.BtwPercentage,
            }).ToList(),
        };

        db.Facturen.Add(credit);
        await db.SaveChangesAsync();
        return credit;
    }

    /// <summary>Som van de totalen van definitieve creditfacturen op een factuur (negatief of 0).</summary>
    public async Task<decimal> GecrediteerdAsync(int factuurId)
    {
        // Optellen in het geheugen: SQLite kan decimals niet betrouwbaar sommeren.
        var bedragen = await db.Facturen
            .Where(f => f.OrigineleFactuurId == factuurId && f.Status == FactuurStatus.Definitief)
            .Select(f => f.TotaalInclBtw)
            .ToListAsync();
        return bedragen.Sum();
    }

    /// <summary>Legt de betaaldatum vast, of maakt de betaling ongedaan met null.</summary>
    public async Task ZetBetaaldAsync(int factuurId, DateOnly? betaaldOp)
    {
        var factuur = await db.Facturen.SingleAsync(f => f.Id == factuurId);
        if (factuur.Status != FactuurStatus.Definitief || factuur.Soort != FactuurSoort.Factuur)
            throw new FactuurException("Alleen bij een definitieve factuur kan de betaling worden vastgelegd.");
        if (betaaldOp < factuur.Factuurdatum)
            throw new FactuurException("De betaaldatum ligt vóór de factuurdatum.");
        factuur.BetaaldOp = betaaldOp;
        await db.SaveChangesAsync();
    }

    /// <summary>
    /// Legt een betalingsherinnering vast. De eerste is een herinnering, daarna volgt de tweede herinnering/aanmaning
    /// (een volgende keer blijft het niveau 2).
    /// </summary>
    public async Task<Herinnering> MaakHerinneringAsync(int factuurId, DateOnly datum, int betalingstermijnDagen)
    {
        var factuur = await db.Facturen.Include(f => f.Herinneringen).SingleAsync(f => f.Id == factuurId);
        var status = factuur.BetaalStatusOp(datum, await GecrediteerdAsync(factuurId));
        if (status is not (BetaalStatus.Openstaand or BetaalStatus.Vervallen))
            throw new FactuurException("Voor deze factuur staat geen bedrag meer open.");
        if (betalingstermijnDagen is < 1 or > 60)
            throw new FactuurException("De termijn van een herinnering moet tussen 1 en 60 dagen liggen.");

        var herinnering = new Herinnering
        {
            FactuurId = factuurId,
            Niveau = Math.Min(2, factuur.Herinneringen.Count + 1),
            Datum = datum,
            BetalingstermijnDagen = betalingstermijnDagen,
        };
        factuur.Herinneringen.Add(herinnering);
        await db.SaveChangesAsync();
        return herinnering;
    }

    /// <summary>
    /// Maakt een nieuwe conceptfactuur met dezelfde klant en regels. Btw-percentages worden bij het
    /// bewerken opnieuw bepaald op de nieuwe factuurdatum.
    /// </summary>
    public async Task<Factuur> MaakKopieAsync(int factuurId, DateOnly factuurdatum, int standaardTermijn)
    {
        var bron = await db.Facturen.Include(f => f.Regels).Include(f => f.Klant).SingleAsync(f => f.Id == factuurId);
        if (bron.Soort != FactuurSoort.Factuur)
            throw new FactuurException("Een creditfactuur kan niet als basis voor een nieuwe factuur dienen.");

        var kopie = new Factuur
        {
            Soort = FactuurSoort.Factuur,
            Status = FactuurStatus.Concept,
            KlantId = bron.KlantId,
            Factuurdatum = factuurdatum,
            BetalingstermijnDagen = bron.Klant?.BetalingstermijnDagen ?? standaardTermijn,
            Taal = bron.Taal,
            Valuta = bron.Valuta,
            BriefpapierId = bron.BriefpapierId,
            Opmerking = bron.Opmerking,
            Regels = bron.Regels.OrderBy(r => r.Volgorde).Select(r => new Factuurregel
            {
                Volgorde = r.Volgorde,
                Soort = r.Soort,
                ArtikelId = r.ArtikelId,
                Artikelnummer = r.Artikelnummer,
                Omschrijving = r.Omschrijving,
                Aantal = r.Aantal,
                Eenheid = r.Eenheid,
                PrijsExclBtw = r.PrijsExclBtw,
                BtwGroepId = r.BtwGroepId,
            }).ToList(),
        };
        db.Facturen.Add(kopie);
        await db.SaveChangesAsync();
        return kopie;
    }
}
