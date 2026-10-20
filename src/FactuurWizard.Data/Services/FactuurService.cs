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
            OrigineleFactuurId = origineel.Id,
            Opmerking = $"Creditering van factuur {origineel.Factuurnummer}.",
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
}
