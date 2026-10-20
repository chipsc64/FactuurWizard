namespace FactuurWizard.Core.Services;

public static class Factuurnummering
{
    /// <summary>
    /// Bepaalt het volgende volgnummer binnen een jaar. Het ingestelde startnummer geldt alleen
    /// voor het eerste jaar; elk later jaar begint bij 1.
    /// </summary>
    public static int VolgendVolgnummer(int? laatsteVolgnummer, int jaar, int eersteJaar, int startnummer)
    {
        if (laatsteVolgnummer is { } laatste)
            return laatste + 1;
        return jaar == eersteJaar ? Math.Max(1, startnummer) : 1;
    }

    public static string Formatteer(int jaar, int volgnummer) => $"{jaar}-{volgnummer:0000}";
}
