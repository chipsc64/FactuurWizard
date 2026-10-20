using FactuurWizard.Core.Model;

namespace FactuurWizard.Core.Services;

public sealed record ControleResultaat(IReadOnlyList<string> Fouten, IReadOnlyList<string> Waarschuwingen)
{
    public bool IsGoed => Fouten.Count == 0;
}

/// <summary>
/// Controleert vóór het definitief maken of de factuur aan de wettelijke vereisten voldoet (art. 35a Wet OB).
/// </summary>
public static class FactuurControle
{
    public static ControleResultaat VoorDefinitief(
        Factuur factuur, Klant klant, Bedrijfsprofiel profiel, DateOnly? laatsteFactuurdatumInJaar, DateOnly vandaag)
    {
        var fouten = new List<string>();
        var waarschuwingen = new List<string>();

        // Leverancier (Bedrijfsgegevens)
        if (string.IsNullOrWhiteSpace(profiel.Bedrijfsnaam))
            fouten.Add("Vul uw bedrijfsnaam in bij Bedrijfsgegevens.");
        if (!IsVolledig(profiel.Adres))
            fouten.Add("Vul uw volledige adres (straat, postcode, plaats) in bij Bedrijfsgegevens.");
        if (string.IsNullOrWhiteSpace(profiel.KvkNummer))
            fouten.Add("Vul uw KvK-nummer in bij Bedrijfsgegevens.");
        if (string.IsNullOrWhiteSpace(profiel.BtwNummer))
            fouten.Add("Vul uw btw-nummer (btw-id) in bij Bedrijfsgegevens.");
        if (string.IsNullOrWhiteSpace(profiel.Iban))
            waarschuwingen.Add("Er is geen IBAN ingevuld bij Bedrijfsgegevens; de klant weet dan niet waar te betalen.");

        // Afnemer
        if (string.IsNullOrWhiteSpace(klant.Naam))
            fouten.Add("De klant heeft geen naam.");
        if (!IsVolledig(klant.EffectiefFactuuradres))
            fouten.Add("Vul het volledige adres van de klant in (straat, postcode, plaats).");

        // Regels
        if (factuur.Regels.Count == 0)
            fouten.Add("De factuur bevat geen regels.");
        foreach (var (regel, nr) in factuur.Regels.OrderBy(r => r.Volgorde).Select((r, i) => (r, i + 1)))
        {
            if (string.IsNullOrWhiteSpace(regel.Omschrijving))
                fouten.Add($"Regel {nr} heeft geen omschrijving.");
            if (regel.Aantal == 0)
                fouten.Add($"Regel {nr} heeft aantal 0.");
        }

        var totaal = FactuurBerekening.Bereken(factuur.Regels).TotaalInclBtw;
        if (factuur.Soort == FactuurSoort.Factuur && totaal < 0)
            waarschuwingen.Add("Het totaal van de factuur is negatief. Gebruik voor een correctie liever een creditfactuur.");
        if (factuur.Soort == FactuurSoort.Creditfactuur && totaal > 0)
            waarschuwingen.Add("Het totaal van de creditfactuur is positief.");

        // Datums
        if (laatsteFactuurdatumInJaar is { } laatste && factuur.Factuurdatum < laatste)
            waarschuwingen.Add($"De factuurdatum ligt vóór die van de vorige factuur ({laatste:dd-MM-yyyy}). " +
                               "Factuurnummers lopen dan niet meer gelijk op met de datum.");
        if (factuur.Factuurdatum > vandaag.AddDays(30))
            waarschuwingen.Add("De factuurdatum ligt meer dan 30 dagen in de toekomst.");

        return new ControleResultaat(fouten, waarschuwingen);
    }

    private static bool IsVolledig(Adres a) =>
        !string.IsNullOrWhiteSpace(a.Straat) && !string.IsNullOrWhiteSpace(a.Postcode) && !string.IsNullOrWhiteSpace(a.Plaats);
}
