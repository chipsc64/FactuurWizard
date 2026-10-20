using FactuurWizard.Core.Model;

namespace FactuurWizard.Core.Services;

public static class BtwTariefBepaling
{
    /// <summary>
    /// Geeft het percentage van de btw-groep dat geldt op de gegeven (factuur)datum.
    /// </summary>
    public static decimal PercentageOp(BtwGroep groep, DateOnly datum)
    {
        var tarief = groep.Tarieven.FirstOrDefault(t => t.GeldigOp(datum))
            ?? throw new InvalidOperationException(
                $"Geen btw-tarief gevonden voor groep '{groep.Naam}' op {datum:dd-MM-yyyy}.");
        return tarief.Percentage;
    }

    /// <summary>
    /// Controleert dat de tariefperiodes van een groep niet overlappen en dat einddatums na de ingangsdatum liggen.
    /// Geeft een lijst met foutmeldingen terug; leeg betekent geldig.
    /// </summary>
    public static IReadOnlyList<string> Valideer(IEnumerable<BtwTarief> tarieven)
    {
        var fouten = new List<string>();
        var gesorteerd = tarieven.OrderBy(t => t.Ingangsdatum).ToList();

        foreach (var t in gesorteerd.Where(t => t.Einddatum < t.Ingangsdatum))
            fouten.Add($"Tarief {t.Percentage}% heeft een einddatum vóór de ingangsdatum.");

        for (var i = 1; i < gesorteerd.Count; i++)
        {
            var vorige = gesorteerd[i - 1];
            var huidige = gesorteerd[i];
            if (vorige.Einddatum is not { } eind || eind >= huidige.Ingangsdatum)
                fouten.Add($"Tarief {vorige.Percentage}% overlapt met tarief {huidige.Percentage}% " +
                           $"(vanaf {huidige.Ingangsdatum:dd-MM-yyyy}).");
        }

        return fouten;
    }
}
