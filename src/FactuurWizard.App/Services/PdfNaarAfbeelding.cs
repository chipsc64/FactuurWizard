using System.IO;
using Windows.Data.Pdf;
using Windows.Storage.Streams;

namespace FactuurWizard.App.Services;

/// <summary>
/// Zet de eerste pagina van een PDF om naar een PNG met de PDF-weergave van Windows,
/// zodat een briefpapier-PDF als achtergrond van de factuur kan dienen.
/// </summary>
public static class PdfNaarAfbeelding
{
    /// <param name="breedtePixels">2480 pixels is A4 op 300 dpi: scherp genoeg om af te drukken.</param>
    public static async Task<byte[]> EerstePaginaAsync(byte[] pdf, uint breedtePixels = 2480)
    {
        using var bron = new MemoryStream(pdf);
        var document = await PdfDocument.LoadFromStreamAsync(bron.AsRandomAccessStream());
        if (document.PageCount == 0)
            throw new InvalidDataException("Het PDF-bestand bevat geen pagina's.");

        using var pagina = document.GetPage(0);
        using var doel = new InMemoryRandomAccessStream();
        await pagina.RenderToStreamAsync(doel, new PdfPageRenderOptions { DestinationWidth = breedtePixels });

        doel.Seek(0);
        using var uit = new MemoryStream();
        await doel.AsStreamForRead().CopyToAsync(uit);
        return uit.ToArray();
    }
}
