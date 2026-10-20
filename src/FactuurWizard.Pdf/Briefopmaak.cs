using System.Globalization;
using FactuurWizard.Core.Model;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace FactuurWizard.Pdf;

/// <summary>
/// Gemeenschappelijke opmaak van facturen en herinneringen: briefpapier, bedrijfsgegevens, adresblok,
/// kenmerken en voettekst. Afgeleide klassen vullen alleen de eigen inhoud.
/// </summary>
internal abstract class Briefopmaak : IDocument
{
    protected static readonly CultureInfo Nl = new("nl-NL");
    protected const string Lijnkleur = "#D5D9DE";
    protected const string Gedempt = "#5F6670";

    private readonly IReadOnlyDictionary<TekstSoort, string> _teksten;
    private readonly string? _watermerk;

    protected Briefopmaak(Factuur factuur, Bedrijfsprofiel profiel, Briefpapier briefpapier,
        IReadOnlyDictionary<TekstSoort, string> teksten, string? watermerk)
    {
        F = factuur;
        Profiel = profiel;
        B = briefpapier;
        _teksten = teksten;
        _watermerk = watermerk;
    }

    protected Factuur F { get; }
    protected Bedrijfsprofiel Profiel { get; }
    protected Briefpapier B { get; }
    protected bool Definitief => F.Status == FactuurStatus.Definitief;

    /// <summary>Bij een definitieve factuur de vastgelegde momentopname, anders de huidige gegevens.</summary>
    protected PartijGegevens Leverancier => Definitief ? F.LeverancierGegevens : PartijGegevens.Van(Profiel);

    protected PartijGegevens Klant =>
        Definitief ? F.KlantGegevens : F.Klant is { } k ? PartijGegevens.Van(k) : new PartijGegevens();

    protected string Iban => Definitief && F.Iban.Length > 0 ? F.Iban : Profiel.Iban;

    public abstract DocumentMetadata GetMetadata();

    /// <summary>Kenmerken rechts naast het adres, zoals factuurnummer en datum.</summary>
    protected abstract IEnumerable<(string Label, string Waarde)> Kenmerken();

    protected abstract void Inhoud(ColumnDescriptor col);

    public void Compose(IDocumentContainer container)
    {
        container.Page(page =>
        {
            page.Size(PageSizes.A4);
            page.MarginTop((float)B.MargeBovenMm, Unit.Millimetre);
            page.MarginBottom((float)B.MargeOnderMm, Unit.Millimetre);
            page.MarginLeft((float)B.MargeLinksMm, Unit.Millimetre);
            page.MarginRight((float)B.MargeRechtsMm, Unit.Millimetre);
            page.DefaultTextStyle(s => s.FontFamily(B.Lettertype, "Segoe UI", "Arial").FontSize(9.5f)
                .FontColor(B.Tekstkleur).LineHeight(1.25f));

            // Een PDF-briefpapier is bij het kiezen al omgezet naar een afbeelding (zie BriefpapierViewModel).
            if (B.AchtergrondSoort == AchtergrondSoort.Afbeelding && B.Achtergrond is { Length: > 0 } afbeelding)
                page.Background().Image(afbeelding).FitArea();

            if (_watermerk is { } watermerk)
                page.Foreground().AlignCenter().AlignMiddle().Rotate(-40)
                    .Text(watermerk).FontSize(90).Bold().FontColor("#22B3261E");

            page.Content().Column(col =>
            {
                col.Spacing(4);
                if (B.ToonBedrijfsgegevens)
                    col.Item().PaddingBottom(14).Element(Bedrijfsblok);

                col.Item().PaddingBottom(18).Row(row =>
                {
                    row.RelativeItem().Element(Klantblok);
                    row.ConstantItem(75, Unit.Millimetre).Element(KenmerkTabel);
                });

                Inhoud(col);
            });
            page.Footer().Element(Voet);
        });
    }

    protected string Tekst(TekstSoort soort) => _teksten.TryGetValue(soort, out var t) ? t.Trim() : "";

    protected void Titel(ColumnDescriptor col, string titel) =>
        col.Item().Text(titel).FontSize(20).SemiBold().FontColor(B.Accentkleur);

    private void Bedrijfsblok(IContainer container)
    {
        var l = Leverancier;
        var rechts = B.BedrijfsgegevensRechts;
        container.Row(row =>
        {
            if (rechts)
                row.RelativeItem();
            row.AutoItem().Column(col =>
            {
                if (B.Logo is { Length: > 0 } logo)
                {
                    var logoItem = col.Item().PaddingBottom(6);
                    (rechts ? logoItem.AlignRight() : logoItem.AlignLeft())
                        .Width((float)B.LogoBreedteMm, Unit.Millimetre).Image(logo).FitWidth();
                }

                void Regel(string tekst, bool vet = false)
                {
                    if (string.IsNullOrWhiteSpace(tekst))
                        return;
                    var item = col.Item();
                    var t = (rechts ? item.AlignRight() : item.AlignLeft()).Text(tekst);
                    if (vet)
                        t.SemiBold().FontSize(11).FontColor(B.Accentkleur);
                }

                Regel(l.Naam, vet: true);
                Regel(l.Adres.StraatRegel);
                Regel(l.Adres.PostcodePlaatsRegel);
                Regel(l.Adres.Land is "" or "Nederland" ? "" : l.Adres.Land);
                Regel(Profiel.Telefoon);
                Regel(l.Email);
                Regel(Profiel.Website);
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
            col.Item().Text(k.Adres.StraatRegel);
            col.Item().Text(k.Adres.PostcodePlaatsRegel);
            if (k.Adres.Land is not ("" or "Nederland"))
                col.Item().Text(k.Adres.Land);
        });
    }

    private void KenmerkTabel(IContainer container)
    {
        container.Table(table =>
        {
            table.ColumnsDefinition(c =>
            {
                c.RelativeColumn(4);
                c.RelativeColumn(5);
            });
            foreach (var (label, waarde) in Kenmerken())
            {
                table.Cell().Text(label).FontColor(Gedempt);
                table.Cell().AlignRight().Text(waarde);
            }
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
            Profiel.Bic.Length > 0 ? $"BIC {Profiel.Bic}" : "",
        }.Where(k => k.Length > 0);

        container.Column(col =>
        {
            if (B.Voettekst.Trim().Length > 0)
                col.Item().AlignCenter().Text(B.Voettekst.Trim()).FontSize(7.5f).FontColor(Gedempt);
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

    protected static string Datum(DateOnly d) => d.ToString("d MMMM yyyy", Nl);
    protected static string Bedrag(decimal b) => b.ToString("C", Nl);
    protected static string Prijs(decimal p) => "€ " + p.ToString("#,##0.00##", Nl);
}
