using Microsoft.EntityFrameworkCore;

namespace FactuurWizard.Data.Services;

public static class Klantnummering
{
    public const int EersteKlantnummer = 1001;

    /// <summary>
    /// Volgend klantnummer, rekening houdend met opgeslagen én nog niet opgeslagen klanten in deze context.
    /// </summary>
    public static async Task<int> VolgendAsync(FactuurWizardDbContext db)
    {
        var hoogsteInDb = await db.Klanten.MaxAsync(k => (int?)k.Klantnummer) ?? 0;
        var hoogsteLokaal = db.Klanten.Local.Select(k => k.Klantnummer).DefaultIfEmpty(0).Max();
        return Math.Max(EersteKlantnummer, Math.Max(hoogsteInDb, hoogsteLokaal) + 1);
    }
}
