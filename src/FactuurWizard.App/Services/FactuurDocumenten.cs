using System.Diagnostics;
using System.IO;
using FactuurWizard.Core.Model;
using FactuurWizard.Data;
using FactuurWizard.Pdf;
using Microsoft.EntityFrameworkCore;

namespace FactuurWizard.App.Services;

/// <summary>
/// Maakt, bewaart en opent de PDF's van facturen. De PDF van een definitieve factuur wordt één keer
/// gemaakt en daarna niet meer overschreven; alleen als het bestand ontbreekt wordt het opnieuw gemaakt.
/// </summary>
public class FactuurDocumenten(Opslaglocatie opslag)
{
    /// <summary>De factuur moet met regels geladen zijn.</summary>
    public async Task<FactuurDocumentInvoer> MaakInvoerAsync(FactuurWizardDbContext db, Factuur factuur)
    {
        var profiel = await db.Bedrijfsprofielen.SingleAsync();
        var briefpapier = await db.Briefpapieren.FirstOrDefaultAsync(b => b.Id == factuur.BriefpapierId)
                          ?? await db.Briefpapieren.OrderByDescending(b => b.IsStandaard).FirstAsync();
        var teksten = await db.Tekstsjablonen.Where(t => t.Taal == factuur.Taal).ToDictionaryAsync(t => t.Soort, t => t.Tekst);
        var klantnummer = factuur.Klant?.Klantnummer
                          ?? await db.Klanten.Where(k => k.Id == factuur.KlantId).Select(k => (int?)k.Klantnummer).FirstOrDefaultAsync();
        var origineelNummer = factuur.OrigineleFactuurId is { } id
            ? await db.Facturen.Where(f => f.Id == id).Select(f => f.Factuurnummer).FirstOrDefaultAsync()
            : null;
        return new FactuurDocumentInvoer(factuur, profiel, briefpapier, teksten, klantnummer, origineelNummer);
    }

    public string? PdfPad(Factuur factuur) =>
        factuur.PdfBestand is { } relatief ? Path.Combine(opslag.DataMap, relatief) : null;

    /// <summary>Geeft het pad van de PDF van een definitieve factuur en maakt hem als hij (nog) niet bestaat.</summary>
    public async Task<string> ZorgVoorPdfAsync(FactuurWizardDbContext db, Factuur factuur)
    {
        if (factuur.Status != FactuurStatus.Definitief)
            throw new InvalidOperationException("Alleen van een definitieve factuur wordt een PDF bewaard.");
        if (PdfPad(factuur) is { } bestaand && File.Exists(bestaand))
            return bestaand;

        var invoer = await MaakInvoerAsync(db, factuur);
        var pdf = await Task.Run(() => FactuurPdf.Genereer(invoer));

        var map = opslag.FactuurMap(factuur.Jaar ?? factuur.Factuurdatum.Year);
        Directory.CreateDirectory(map);
        var pad = UniekPad(map, $"{factuur.Factuurnummer} {factuur.KlantGegevens.Naam}");
        await File.WriteAllBytesAsync(pad, pdf);

        factuur.PdfBestand = Path.GetRelativePath(opslag.DataMap, pad);
        await db.SaveChangesAsync();
        return pad;
    }

    /// <summary>Maakt een tijdelijke PDF (met watermerk) van een concept of voorbeeld.</summary>
    public async Task<string> MaakTijdelijkePdfAsync(FactuurDocumentInvoer invoer, string naam)
    {
        var pdf = await Task.Run(() => FactuurPdf.Genereer(invoer));
        var map = Path.Combine(Path.GetTempPath(), "FactuurWizard");
        Directory.CreateDirectory(map);
        var pad = Path.Combine(map, $"{Bestandsnaam(naam)} {DateTime.Now:HHmmss}.pdf");
        await File.WriteAllBytesAsync(pad, pdf);
        return pad;
    }

    public static void Open(string pad) => Process.Start(new ProcessStartInfo(pad) { UseShellExecute = true });

    public static void ToonInVerkenner(string pad) => Process.Start("explorer.exe", $"/select,\"{pad}\"");

    private static string UniekPad(string map, string naam)
    {
        var basis = Bestandsnaam(naam);
        var pad = Path.Combine(map, basis + ".pdf");
        for (var i = 2; File.Exists(pad); i++)
            pad = Path.Combine(map, $"{basis} ({i}).pdf");
        return pad;
    }

    private static string Bestandsnaam(string naam)
    {
        var ongeldig = Path.GetInvalidFileNameChars();
        var schoon = new string(naam.Select(c => ongeldig.Contains(c) ? '_' : c).ToArray()).Trim();
        return schoon.Length > 80 ? schoon[..80].Trim() : schoon;
    }
}
