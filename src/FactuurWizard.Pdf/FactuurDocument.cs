using FactuurWizard.Core.Model;
using FactuurWizard.Core.Services;
using QuestPDF.Fluent;
using QuestPDF.Infrastructure;

namespace FactuurWizard.Pdf;

/// <summary>
/// Alles wat nodig is om een factuur op te maken.
/// </summary>
public sealed record FactuurDocumentInvoer(
    Factuur Factuur,
    Bedrijfsprofiel Profiel,
    Briefpapier Briefpapier,
    IReadOnlyDictionary<TekstSoort, string> Teksten,
    int? Klantnummer = null,
    string? OrigineelFactuurnummer = null,
    string? Watermerk = null);

/// <summary>
/// Alles wat nodig is om een betalingsherinnering op te maken.
/// </summary>
public sealed record HerinneringDocumentInvoer(
    Factuur Factuur,
    Herinnering Herinnering,
    decimal OpenstaandBedrag,
    Bedrijfsprofiel Profiel,
    Briefpapier Briefpapier,
    IReadOnlyDictionary<TekstSoort, string> Teksten,
    int? Klantnummer = null);

public static class FactuurPdf
{
    static FactuurPdf()
    {
        QuestPDF.Settings.License = LicenseType.Community;
        // Het lettertype wordt gekozen uit de lettertypen die op de pc van de gebruiker staan.
        QuestPDF.Settings.UseSystemFonts = true;
        // Is het gekozen lettertype later verwijderd, dan valt de opmaak terug op een reservelettertype.
        QuestPDF.Settings.ThrowOnMissingFontFamilies = false;
    }

    /// <summary>Maakt de factuur als PDF (A4). Een concept krijgt automatisch het watermerk "CONCEPT".</summary>
    public static byte[] Genereer(FactuurDocumentInvoer invoer) => new FactuurDocument(MetWatermerk(invoer)).GeneratePdf();

    /// <summary>Maakt per pagina een PNG-afbeelding, bijvoorbeeld voor een voorbeeldweergave.</summary>
    public static IReadOnlyList<byte[]> GenereerAfbeeldingen(FactuurDocumentInvoer invoer, int dpi = 96) =>
        new FactuurDocument(MetWatermerk(invoer)).GenerateImages(new ImageGenerationSettings { RasterDpi = dpi }).ToList();

    public static byte[] GenereerHerinnering(HerinneringDocumentInvoer invoer) => new HerinneringDocument(invoer).GeneratePdf();

    public static IReadOnlyList<byte[]> GenereerHerinneringAfbeeldingen(HerinneringDocumentInvoer invoer, int dpi = 96) =>
        new HerinneringDocument(invoer).GenerateImages(new ImageGenerationSettings { RasterDpi = dpi }).ToList();

    private static FactuurDocumentInvoer MetWatermerk(FactuurDocumentInvoer invoer) =>
        invoer.Watermerk is null && invoer.Factuur.Status == FactuurStatus.Concept ? invoer with { Watermerk = "CONCEPT" } : invoer;
}

internal sealed class FactuurDocument(FactuurDocumentInvoer invoer)
    : Briefopmaak(invoer.Factuur, invoer.Profiel, invoer.Briefpapier, invoer.Teksten, invoer.Watermerk)
{
    private readonly FactuurTotalen _totalen = FactuurBerekening.Bereken(invoer.Factuur.Regels);

    private bool IsCredit => F.Soort == FactuurSoort.Creditfactuur;

    private bool Kor => Definitief
        ? F.KorToegepast
        : F.Regels.All(r => r.BtwPercentage == 0) && Profiel.KorVanToepassing(F.Factuurdatum);

    public override DocumentMetadata GetMetadata() => new()
    {
        Title = $"{(IsCredit ? "Creditfactuur" : "Factuur")} {F.Factuurnummer}".Trim(),
        Author = Leverancier.Naam,
        Creator = "FactuurWizard",
    };

    protected override IEnumerable<(string Label, string Waarde)> Kenmerken()
    {
        yield return (IsCredit ? "Creditfactuurnummer" : "Factuurnummer", F.Factuurnummer ?? "(concept)");
        yield return ("Factuurdatum", Datum(F.Factuurdatum));
        if (!IsCredit)
            yield return ("Vervaldatum", Datum(F.Vervaldatum));
        if (F.Leverdatum is { } van)
            yield return ("Leverdatum", F.LeverperiodeEind is { } tot ? $"{Datum(van)} t/m {Datum(tot)}" : Datum(van));
        if (invoer.Klantnummer is { } klantnummer)
            yield return ("Klantnummer", klantnummer.ToString());
        if (Klant.BtwNummer.Length > 0)
            yield return ("Uw btw-nummer", Klant.BtwNummer);
    }

    protected override void Inhoud(ColumnDescriptor col)
    {
        Titel(col, IsCredit ? "Creditfactuur" : "Factuur");
        if (IsCredit && invoer.OrigineelFactuurnummer is { } origineel)
            col.Item().Text($"Creditering van factuur {origineel}").FontColor(Gedempt);

        col.Item().PaddingTop(8).Element(Regeltabel);
        col.Item().AlignRight().Width(85, Unit.Millimetre).PaddingTop(6).Element(Totalen);

        if (Kor)
            col.Item().PaddingTop(6).Text("Vrijgesteld van btw op grond van de kleineondernemersregeling (KOR).").Italic();

        if (F.Opmerking.Trim().Length > 0)
            col.Item().PaddingTop(10).Text(F.Opmerking.Trim());

        var waarden = Plaatshouders.VoorFactuur(F, Profiel);
        var voorwaarden = IsCredit
            ? "Het creditbedrag wordt met u verrekend of aan u terugbetaald."
            : Plaatshouders.Vul(Tekst(TekstSoort.Betalingsvoorwaarden), waarden);
        if (voorwaarden.Length > 0)
            col.Item().PaddingTop(12).Text(voorwaarden);

        var afsluiting = Plaatshouders.Vul(Tekst(TekstSoort.Afsluittekst), waarden);
        if (afsluiting.Length > 0)
            col.Item().PaddingTop(6).Text(afsluiting);

        var algemeen = Tekst(TekstSoort.AlgemeneVoorwaarden);
        if (algemeen.Length > 0)
            col.Item().PaddingTop(14).Text(algemeen).FontSize(7).FontColor(Gedempt);
    }

    private void Regeltabel(IContainer container)
    {
        var metArtikelnummer = F.Regels.Any(r => r.Artikelnummer.Length > 0);
        var regels = F.Regels.OrderBy(r => r.Volgorde).ToList();

        container.Table(table =>
        {
            table.ColumnsDefinition(c =>
            {
                if (metArtikelnummer)
                    c.ConstantColumn(16, Unit.Millimetre);
                c.RelativeColumn();
                c.ConstantColumn(15, Unit.Millimetre);
                c.ConstantColumn(15, Unit.Millimetre);
                c.ConstantColumn(23, Unit.Millimetre);
                if (!Kor)
                    c.ConstantColumn(12, Unit.Millimetre);
                c.ConstantColumn(25, Unit.Millimetre);
            });

            table.Header(h =>
            {
                if (metArtikelnummer)
                    Kop(h.Cell(), "Art.nr.");
                Kop(h.Cell(), "Omschrijving");
                Kop(h.Cell(), "Aantal", rechts: true);
                Kop(h.Cell(), "Eenheid", inspringen: true);
                Kop(h.Cell(), "Prijs", rechts: true);
                if (!Kor)
                    Kop(h.Cell(), "Btw", rechts: true);
                Kop(h.Cell(), "Bedrag", rechts: true);
            });

            foreach (var r in regels)
            {
                if (metArtikelnummer)
                    Cel(table.Cell()).Text(r.Artikelnummer);
                Cel(table.Cell()).Text(r.Omschrijving);
                Cel(table.Cell()).AlignRight().Text(r.Aantal.ToString("0.##", Nl));
                Cel(table.Cell()).PaddingLeft(6).Text(r.Eenheid);
                Cel(table.Cell()).AlignRight().Text(Prijs(r.PrijsExclBtw));
                if (!Kor)
                    Cel(table.Cell()).AlignRight().Text($"{r.BtwPercentage:0.##}%");
                Cel(table.Cell()).AlignRight().Text(Bedrag(FactuurBerekening.Regelbedrag(r)));
            }
        });

        // Eerst rand en opvulling, dan pas uitlijnen: anders loopt de lijn niet over de volle kolombreedte.
        void Kop(IContainer cel, string tekst, bool rechts = false, bool inspringen = false)
        {
            var c = cel.BorderBottom(1).BorderColor(B.Accentkleur).PaddingVertical(4).PaddingLeft(inspringen ? 6 : 0);
            (rechts ? c.AlignRight() : c).Text(tekst).SemiBold().FontColor(B.Accentkleur);
        }

        static IContainer Cel(IContainer cel) => cel.BorderBottom(0.5f).BorderColor(Lijnkleur).PaddingVertical(4);
    }

    private void Totalen(IContainer container)
    {
        container.Table(table =>
        {
            table.ColumnsDefinition(c =>
            {
                c.RelativeColumn();
                c.ConstantColumn(28, Unit.Millimetre);
            });

            void Regel(string label, decimal bedrag, bool totaal = false)
            {
                if (totaal)
                {
                    table.Cell().BorderTop(1).BorderColor(B.Accentkleur).PaddingTop(4).Text(label).SemiBold().FontSize(11);
                    table.Cell().BorderTop(1).BorderColor(B.Accentkleur).PaddingTop(4).AlignRight()
                        .Text(Bedrag(bedrag)).SemiBold().FontSize(11);
                    return;
                }
                table.Cell().PaddingVertical(2).Text(label);
                table.Cell().PaddingVertical(2).AlignRight().Text(Bedrag(bedrag));
            }

            Regel("Subtotaal excl. btw", _totalen.TotaalExclBtw);
            if (!Kor)
                foreach (var s in _totalen.PerTarief)
                    Regel($"Btw {s.Percentage:0.##}% over {Bedrag(s.Grondslag)}", s.Btw);
            Regel(IsCredit ? "Totaal te crediteren" : "Totaal te betalen", _totalen.TotaalInclBtw, totaal: true);
        });
    }
}
