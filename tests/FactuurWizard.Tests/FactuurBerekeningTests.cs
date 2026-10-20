using FactuurWizard.Core.Model;
using FactuurWizard.Core.Services;

namespace FactuurWizard.Tests;

public class FactuurBerekeningTests
{
    private static Factuurregel Regel(decimal aantal, decimal prijs, int groep, decimal pct) =>
        new() { Aantal = aantal, PrijsExclBtw = prijs, BtwGroepId = groep, BtwPercentage = pct };

    [Fact]
    public void Btw_wordt_per_tarief_over_het_totaal_berekend()
    {
        // Per regel afgerond: 3 × round(0,105) = 3 × 0,11 = 0,33; over totaal: round(0,315) = 0,32.
        var totalen = FactuurBerekening.Bereken(
        [
            Regel(1, 0.50m, 1, 21m),
            Regel(1, 0.50m, 1, 21m),
            Regel(1, 0.50m, 1, 21m),
        ]);

        Assert.Equal(1.50m, totalen.TotaalExclBtw);
        Assert.Equal(0.32m, totalen.TotaalBtw);
        Assert.Equal(1.82m, totalen.TotaalInclBtw);
    }

    [Fact]
    public void Meerdere_tarieven_worden_apart_gespecificeerd()
    {
        var totalen = FactuurBerekening.Bereken(
        [
            Regel(2, 100m, 1, 21m),
            Regel(3, 10m, 2, 9m),
            Regel(1, 50m, 3, 0m),
        ]);

        Assert.Equal(3, totalen.PerTarief.Count);
        Assert.Equal(new BtwSpecificatie(1, 21m, 200m, 42m), totalen.PerTarief[0]);
        Assert.Equal(new BtwSpecificatie(2, 9m, 30m, 2.70m), totalen.PerTarief[1]);
        Assert.Equal(new BtwSpecificatie(3, 0m, 50m, 0m), totalen.PerTarief[2]);
        Assert.Equal(280m, totalen.TotaalExclBtw);
        Assert.Equal(44.70m, totalen.TotaalBtw);
        Assert.Equal(324.70m, totalen.TotaalInclBtw);
    }

    [Fact]
    public void Creditbedragen_worden_symmetrisch_afgerond()
    {
        var factuur = FactuurBerekening.Bereken([Regel(1, 1.50m, 1, 21m)]);
        var credit = FactuurBerekening.Bereken([Regel(-1, 1.50m, 1, 21m)]);

        Assert.Equal(-factuur.TotaalInclBtw, credit.TotaalInclBtw);
    }

    [Fact]
    public void Regelbedrag_wordt_op_centen_afgerond()
    {
        Assert.Equal(12.35m, FactuurBerekening.Regelbedrag(Regel(2.5m, 4.94m, 1, 21m)));
    }
}
