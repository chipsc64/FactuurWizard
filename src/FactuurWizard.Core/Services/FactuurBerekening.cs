using FactuurWizard.Core.Model;

namespace FactuurWizard.Core.Services;

public sealed record BtwSpecificatie(int BtwGroepId, decimal Percentage, decimal Grondslag, decimal Btw);

public sealed record FactuurTotalen(
    IReadOnlyList<BtwSpecificatie> PerTarief,
    decimal TotaalExclBtw,
    decimal TotaalBtw,
    decimal TotaalInclBtw);

public static class FactuurBerekening
{
    public static decimal Rond(decimal bedrag) => Math.Round(bedrag, 2, MidpointRounding.AwayFromZero);

    /// <summary>
    /// Regelbedrag exclusief btw. Uitbreidingspunt voor kortingen per regel.
    /// </summary>
    public static decimal Regelbedrag(Factuurregel regel) => Rond(regel.Aantal * regel.PrijsExclBtw);

    /// <summary>
    /// Berekent de totalen. De btw wordt per tarief over het totaal van de regels berekend en daarna afgerond.
    /// </summary>
    public static FactuurTotalen Bereken(IEnumerable<Factuurregel> regels)
    {
        var perTarief = regels
            .GroupBy(r => (r.BtwGroepId, r.BtwPercentage))
            .Select(g =>
            {
                var grondslag = g.Sum(Regelbedrag);
                return new BtwSpecificatie(g.Key.BtwGroepId, g.Key.BtwPercentage, grondslag,
                    Rond(grondslag * g.Key.BtwPercentage / 100m));
            })
            .OrderByDescending(s => s.Percentage)
            .ToList();

        var excl = perTarief.Sum(s => s.Grondslag);
        var btw = perTarief.Sum(s => s.Btw);
        return new FactuurTotalen(perTarief, excl, btw, excl + btw);
    }
}
