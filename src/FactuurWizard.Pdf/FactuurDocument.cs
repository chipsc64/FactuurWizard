using System.Globalization;
using FactuurWizard.Core.Model;
using FactuurWizard.Core.Services;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
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
    public static byte[] Genereer(FactuurDocumentInvoer invoer)
    {
        if (invoer.Watermerk is null && invoer.Factuur.Status == FactuurStatus.Concept)
            invoer = invoer with { Watermerk = "CONCEPT" };

        return new FactuurDocument(invoer).GeneratePdf();
    }

    /// <summary>Maakt per pagina een PNG-afbeelding, bijvoorbeeld voor een voorbeeldweergave.</summary>
    public static IReadOnlyList<byte[]> GenereerAfbeeldingen(FactuurDocumentInvoer invoer, int dpi = 96)
    {
        if (invoer.Watermerk is null && invoer.Factuur.Status == FactuurStatus.Concept)
            invoer = invoer with { Watermerk = "CONCEPT" };
        return new FactuurDocument(invoer).GenerateImages(new ImageGenerationSettings { RasterDpi = dpi }).ToList();
    }
}

internal sealed class FactuurDocument(FactuurDocumentInvoer invoer) : IDocument
{
    private static readonly CultureInfo Nl = new("nl-NL");
    private const string Lijnkleur = "#D5D9DE";
    private const string Gedempt = "#5F6670";

    private readonly Factuur _f = invoer.Factuur;
    private readonly Briefpapier _b = invoer.Briefpapier;
    private readonly Bedrijfsprofiel _profiel = invoer.Profiel;
    private readonly bool _definitief = invoer.Factuur.Status == FactuurStatus.Definitief;
    private readonly FactuurTotalen _totalen = FactuurBerekening.Bereken(invoer.Factuur.Regels);

    private PartijGegevens Leverancier => _definitief ? _f.LeverancierGegevens : PartijGegevens.Van(_profiel);

    private PartijGegevens Klant =>
        _definitief ? _f.KlantGegevens : _f.Klant is { } k ? PartijGegevens.Van(k) : new PartijGegevens();

    private string Iban => _definitief && _f.Iban.Length > 0 ? _f.Iban : _profiel.Iban;
    private bool IsCredit => _f.Soort == FactuurSoort.Creditfactuur;
    private bool Kor => _definitief ? _f.KorToegepast : _f.Regels.All(r => r.BtwPercentage == 0) && _profiel.KorVanToepassing(_f.Factuurdatum);

    public DocumentMetadata GetMetadata() => new()
    {
        Title = $"{(IsCredit ? "Creditfactuur" : "Factuur")} {_f.Factuurnummer}".Trim(),
        Author = Leverancier.Naam,
        Creator = "FactuurWizard",
    };

    public void Compose(IDocumentContainer container)
    {
        container.Page(page =>
        {
            page.Size(PageSizes.A4);
            page.MarginTop((float)_b.MargeBovenMm, Unit.Millimetre);
            page.MarginBottom((float)_b.MargeOnderMm, Unit.Millimetre);
            page.MarginLeft((float)_b.MargeLinksMm, Unit.Millimetre);
            page.MarginRight((float)_b.MargeRechtsMm, Unit.Millimetre);
            page.DefaultTextStyle(s => s.FontFamily(_b.Lettertype, "Segoe UI", "Arial").FontSize(9.5f).FontColor(_b.Tekstkleur).LineHeight(1.25f));

            // Een PDF-briefpapier is bij het kiezen al omgezet naar een afbeelding (zie BriefpapierViewModel).
            if (_b.AchtergrondSoort == AchtergrondSoort.Afbeelding && _b.Achtergrond is { Length: > 0 } afbeelding)
                page.Background().Image(afbeelding).FitArea();

            if (invoer.Watermerk is { } watermerk)
                page.Foreground().AlignCenter().AlignMiddle().Rotate(-40)
                    .Text(watermerk).FontSize(90).Bold().FontColor("#22B3261E");

            page.Content().Element(Inhoud);
            page.Footer().Element(Voet);
        });
    }

    private void Inhoud(IContainer container)
    {
        container.Column(col =>
        {
            col.Spacing(4);
            if (_b.ToonBedrijfsgegevens)
                col.Item().PaddingBottom(14).Element(Bedrijfsblok);

            col.Item().PaddingBottom(18).Row(row =>
            {
                row.RelativeItem().Element(Klantblok);
                row.ConstantItem(75, Unit.Millimetre).Element(Kenmerken);
            });

            col.Item().Text(IsCredit ? "Creditfactuur" : "Factuur").FontSize(20).SemiBold().FontColor(_b.Accentkleur);
            if (IsCredit && invoer.OrigineelFactuurnummer is { } origineel)
                col.Item().Text($"Creditering van factuur {origineel}").FontColor(Gedempt);

            col.Item().PaddingTop(8).Element(Regeltabel);
            col.Item().AlignRight().Width(85, Unit.Millimetre).PaddingTop(6).Element(Totalen);

            if (Kor)
                col.Item().PaddingTop(6).Text("Vrijgesteld van btw op grond van de kleineondernemersregeling (KOR).").Italic();

            if (_f.Opmerking.Trim().Length > 0)
                col.Item().PaddingTop(10).Text(_f.Opmerking.Trim());

            var waarden = Plaatshouders.VoorFactuur(_f, _profiel);
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
        });
    }

    private string Tekst(TekstSoort soort) => invoer.Teksten.TryGetValue(soort, out var t) ? t.Trim() : "";

    private void Bedrijfsblok(IContainer container)
    {
        var l = Leverancier;
        var rechts = _b.BedrijfsgegevensRechts;
        container.Row(row =>
        {
            if (rechts)
                row.RelativeItem();
            row.AutoItem().Column(col =>
            {
                if (_b.Logo is { Length: > 0 } logo)
                {
                    var logoItem = col.Item().PaddingBottom(6);
                    (rechts ? logoItem.AlignRight() : logoItem.AlignLeft())
                        .Width((float)_b.LogoBreedteMm, Unit.Millimetre).Image(logo).FitWidth();
                }

                void Regel(string tekst, bool vet = false)
                {
                    if (string.IsNullOrWhiteSpace(tekst))
                        return;
                    var item = col.Item();
                    var t = (rechts ? item.AlignRight() : item.AlignLeft()).Text(tekst);
                    if (vet)
                        t.SemiBold().FontSize(11).FontColor(_b.Accentkleur);
                }

                Regel(l.Naam, vet: true);
                Regel($"{l.Adres.Straat} {l.Adres.Huisnummer}".Trim());
                Regel($"{l.Adres.Postcode} {l.Adres.Plaats}".Trim());
                Regel(l.Adres.Land is "" or "Nederland" ? "" : l.Adres.Land);
                Regel(_profiel.Telefoon);
                Regel(l.Email);
                Regel(_profiel.Website);
            });
            if (!rechts)
                row.RelativeItem();
        });
    }

    private void Klantblok(IContainer container)
    {
        var k = Klant;
        container.Column(col =>
        {
            col.Item().Text(k.Naam).SemiBold();
            if (k.Contactpersoon.Length > 0)
                col.Item().Text($"t.a.v. {k.Contactpersoon}");
            col.Item().Text($"{k.Adres.Straat} {k.Adres.Huisnummer}".Trim());
            col.Item().Text($"{k.Adres.Postcode} {k.Adres.Plaats}".Trim());
            if (k.Adres.Land is not ("" or "Nederland"))
                col.Item().Text(k.Adres.Land);
        });
    }

    private void Kenmerken(IContainer container)
    {
        var regels = new List<(string Label, string Waarde)>
        {
            (IsCredit ? "Creditfactuurnummer" : "Factuurnummer", _f.Factuurnummer ?? "(concept)"),
            ("Factuurdatum", Datum(_f.Factuurdatum)),
        };
        if (!IsCredit)
            regels.Add(("Vervaldatum", Datum(_f.Vervaldatum)));
        if (_f.Leverdatum is { } van)
            regels.Add(("Leverdatum", _f.LeverperiodeEind is { } tot ? $"{Datum(van)} t/m {Datum(tot)}" : Datum(van)));
        if (invoer.Klantnummer is { } klantnummer)
            regels.Add(("Klantnummer", klantnummer.ToString()));
        if (Klant.BtwNummer.Length > 0)
            regels.Add(("Uw btw-nummer", Klant.BtwNummer));

        container.Table(table =>
        {
            table.ColumnsDefinition(c =>
            {
                c.RelativeColumn(4);
                c.RelativeColumn(5);
            });
            foreach (var (label, waarde) in regels)
            {
                table.Cell().Text(label).FontColor(Gedempt);
                table.Cell().AlignRight().Text(waarde);
            }
        });
    }

    private void Regeltabel(IContainer container)
    {
        var metArtikelnummer = _f.Regels.Any(r => r.Artikelnummer.Length > 0);
        var regels = _f.Regels.OrderBy(r => r.Volgorde).ToList();

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
            var c = cel.BorderBottom(1).BorderColor(_b.Accentkleur).PaddingVertical(4).PaddingLeft(inspringen ? 6 : 0);
            (rechts ? c.AlignRight() : c).Text(tekst).SemiBold().FontColor(_b.Accentkleur);
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
                var l = table.Cell().PaddingVertical(2);
                var w = table.Cell().PaddingVertical(2).AlignRight();
                if (totaal)
                {
                    l = table.Cell().BorderTop(1).BorderColor(_b.Accentkleur).PaddingTop(4);
                    w = table.Cell().BorderTop(1).BorderColor(_b.Accentkleur).PaddingTop(4).AlignRight();
                    l.Text(label).SemiBold().FontSize(11);
                    w.Text(Bedrag(bedrag)).SemiBold().FontSize(11);
                    return;
                }
                l.Text(label);
                w.Text(Bedrag(bedrag));
            }

            Regel("Subtotaal excl. btw", _totalen.TotaalExclBtw);
            if (!Kor)
                foreach (var s in _totalen.PerTarief)
                    Regel($"Btw {s.Percentage:0.##}% over {Bedrag(s.Grondslag)}", s.Btw);
            Regel(IsCredit ? "Totaal te crediteren" : "Totaal te betalen", _totalen.TotaalInclBtw, totaal: true);
        });
    }

    private void Voet(IContainer container)
    {
        var l = Leverancier;
        var kenmerken = new[]
        {
            l.Naam,
            l.KvkNummer.Length > 0 ? $"KvK {l.KvkNummer}" : "",
            l.BtwNummer.Length > 0 ? $"Btw-id {l.BtwNummer}" : "",
            Iban.Length > 0 ? $"IBAN {Iban}" : "",
            _profiel.Bic.Length > 0 ? $"BIC {_profiel.Bic}" : "",
        }.Where(k => k.Length > 0);

        container.Column(col =>
        {
            if (_b.Voettekst.Trim().Length > 0)
                col.Item().AlignCenter().Text(_b.Voettekst.Trim()).FontSize(7.5f).FontColor(Gedempt);
            col.Item().AlignCenter().Text(string.Join("  ·  ", kenmerken)).FontSize(7.5f).FontColor(Gedempt);
            col.Item().AlignCenter().Text(t =>
            {
                t.DefaultTextStyle(s => s.FontSize(7.5f).FontColor(Gedempt));
                t.Span("Pagina ");
                t.CurrentPageNumber();
                t.Span(" van ");
                t.TotalPages();
            });
        });
    }

    private static string Datum(DateOnly d) => d.ToString("d MMMM yyyy", Nl);
    private static string Bedrag(decimal b) => b.ToString("C", Nl);
    private static string Prijs(decimal p) => "€ " + p.ToString("#,##0.00##", Nl);
}
