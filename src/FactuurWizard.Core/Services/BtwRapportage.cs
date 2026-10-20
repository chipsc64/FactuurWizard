using FactuurWizard.Core.Model;

namespace FactuurWizard.Core.Services;

public sealed record RubriekRegel(string Code, string Omschrijving, decimal Omzet, decimal Btw);

/// <summary>Btw-specificatie van één factuur, verdeeld over de aangifterubrieken.</summary>
public sealed record FactuurBtwRegel(
    Factuur Factuur,
    decimal Omzet1a, decimal Btw1a,
    decimal Omzet1b, decimal Btw1b,
    decimal Omzet1e,
    decimal OmzetKor)
{
    public decimal TotaalExclBtw => Omzet1a + Omzet1b + Omzet1e + OmzetKor;
    public decimal TotaalBtw => Btw1a + Btw1b;
    public decimal TotaalInclBtw => TotaalExclBtw + TotaalBtw;
}

public sealed record BtwOverzicht(
    DateOnly Van,
    DateOnly Tot,
    IReadOnlyList<RubriekRegel> Rubrieken,
    decimal VerschuldigdeBtw,
    decimal OmzetKor,
    IReadOnlyList<FactuurBtwRegel> Facturen);

/// <summary>
/// Btw-overzicht volgens het factuurstelsel: alle definitieve facturen en creditfacturen met een
/// factuurdatum in de periode, ingedeeld in de rubrieken van de btw-aangifte.
/// </summary>
public static class BtwRapportage
{
    public const string Omschrijving1a = "Leveringen/diensten belast met hoog tarief";
    public const string Omschrijving1b = "Leveringen/diensten belast met laag tarief";
    public const string Omschrijving1e = "Leveringen/diensten belast met 0% of niet bij u belast";
    public const string Omschrijving5a = "Verschuldigde omzetbelasting (rubrieken 1a t/m 4b)";

    /// <summary>Begin en einde van een kwartaal (1–4), of van het hele jaar als kwartaal null is.</summary>
    public static (DateOnly Van, DateOnly Tot) Periode(int jaar, int? kwartaal)
    {
        if (kwartaal is not { } k)
            return (new DateOnly(jaar, 1, 1), new DateOnly(jaar, 12, 31));
        if (k is < 1 or > 4)
            throw new ArgumentOutOfRangeException(nameof(kwartaal));
        var van = new DateOnly(jaar, (k - 1) * 3 + 1, 1);
        return (van, van.AddMonths(3).AddDays(-1));
    }

    /// <param name="groepSoorten">Soort (hoog/laag/nul) per btw-groep-id.</param>
    public static BtwOverzicht Bereken(
        IEnumerable<Factuur> facturen, IReadOnlyDictionary<int, BtwSoort> groepSoorten, DateOnly van, DateOnly tot)
    {
        var regels = facturen
            .Where(f => f.Status == FactuurStatus.Definitief && f.Factuurdatum >= van && f.Factuurdatum <= tot)
            .OrderBy(f => f.Jaar).ThenBy(f => f.Volgnummer)
            .Select(f => Specificeer(f, groepSoorten))
            .ToList();

        var rubrieken = new List<RubriekRegel>
        {
            new("1a", Omschrijving1a, regels.Sum(r => r.Omzet1a), regels.Sum(r => r.Btw1a)),
            new("1b", Omschrijving1b, regels.Sum(r => r.Omzet1b), regels.Sum(r => r.Btw1b)),
            new("1e", Omschrijving1e, regels.Sum(r => r.Omzet1e), 0m),
        };

        return new BtwOverzicht(van, tot, rubrieken, rubrieken.Sum(r => r.Btw), regels.Sum(r => r.OmzetKor), regels);
    }

    /// <summary>
    /// Verdeelt de btw-specificatie van de factuur (zoals op de factuur afgerond) over de rubrieken.
    /// </summary>
    public static FactuurBtwRegel Specificeer(Factuur factuur, IReadOnlyDictionary<int, BtwSoort> groepSoorten)
    {
        var totalen = FactuurBerekening.Bereken(factuur.Regels);
        if (factuur.KorToegepast)
            return new FactuurBtwRegel(factuur, 0, 0, 0, 0, 0, totalen.TotaalExclBtw);

        decimal o1a = 0, b1a = 0, o1b = 0, b1b = 0, o1e = 0;
        foreach (var s in totalen.PerTarief)
        {
            var soort = s.Percentage == 0 ? BtwSoort.Nul : groepSoorten.GetValueOrDefault(s.BtwGroepId, BtwSoort.Hoog);
            switch (soort)
            {
                case BtwSoort.Hoog:
                    o1a += s.Grondslag;
                    b1a += s.Btw;
                    break;
                case BtwSoort.Laag:
                    o1b += s.Grondslag;
                    b1b += s.Btw;
                    break;
                default:
                    o1e += s.Grondslag;
                    break;
            }
        }
        return new FactuurBtwRegel(factuur, o1a, b1a, o1b, b1b, o1e, 0);
    }

    /// <summary>In de aangifte vult u hele euro's in; afronden naar beneden is in uw voordeel.</summary>
    public static decimal HeleEuros(decimal bedrag) => Math.Floor(bedrag);
}
