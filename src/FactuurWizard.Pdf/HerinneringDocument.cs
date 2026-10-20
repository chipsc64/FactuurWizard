using FactuurWizard.Core.Model;
using FactuurWizard.Core.Services;
using QuestPDF.Fluent;
using QuestPDF.Infrastructure;

namespace FactuurWizard.Pdf;

/// <summary>
/// Betalingsherinnering (niveau 1) of tweede herinnering/aanmaning (niveau 2) voor een definitieve factuur.
/// </summary>
internal sealed class HerinneringDocument(HerinneringDocumentInvoer invoer)
    : Briefopmaak(invoer.Factuur, invoer.Profiel, invoer.Briefpapier, invoer.Teksten, watermerk: null)
{
    private Herinnering H => invoer.Herinnering;
    private string Soort => H.Niveau >= 2 ? "Tweede herinnering" : "Betalingsherinnering";

    public override DocumentMetadata GetMetadata() => new()
    {
        Title = $"{Soort} factuur {F.Factuurnummer}",
        Author = Leverancier.Naam,
        Creator = "FactuurWizard",
    };

    protected override IEnumerable<(string Label, string Waarde)> Kenmerken()
    {
        yield return ("Datum", Datum(H.Datum));
        yield return ("Betreft factuur", F.Factuurnummer ?? "");
        if (invoer.Klantnummer is { } klantnummer)
            yield return ("Klantnummer", klantnummer.ToString());
    }

    protected override void Inhoud(ColumnDescriptor col)
    {
        Titel(col, Soort);

        var waarden = Plaatshouders.VoorFactuur(F, Profiel, H.BetalingstermijnDagen);
        waarden["TotaalInclBtw"] = Bedrag(invoer.OpenstaandBedrag);

        col.Item().PaddingTop(10).Text($"Beste {waarden["Klantnaam"]},");
        var tekst = Plaatshouders.Vul(Tekst(H.Niveau >= 2 ? TekstSoort.Herinnering2Tekst : TekstSoort.Herinnering1Tekst), waarden);
        col.Item().PaddingTop(6).Text(tekst);

        col.Item().PaddingTop(14).Table(table =>
        {
            table.ColumnsDefinition(c =>
            {
                c.RelativeColumn();
                c.RelativeColumn();
                c.RelativeColumn();
                c.RelativeColumn();
            });

            void Kop(string t, bool rechts = false)
            {
                var c = table.Cell().BorderBottom(1).BorderColor(B.Accentkleur).PaddingVertical(4);
                (rechts ? c.AlignRight() : c).Text(t).SemiBold().FontColor(B.Accentkleur);
            }

            IContainer Cel() => table.Cell().BorderBottom(0.5f).BorderColor(Lijnkleur).PaddingVertical(4);

            Kop("Factuurnummer");
            Kop("Factuurdatum");
            Kop("Vervaldatum");
            Kop("Openstaand", rechts: true);
            Cel().Text(F.Factuurnummer ?? "");
            Cel().Text(Datum(F.Factuurdatum));
            Cel().Text(Datum(F.Vervaldatum));
            Cel().AlignRight().Text(Bedrag(invoer.OpenstaandBedrag)).SemiBold();
        });

        col.Item().PaddingTop(16).Text("Met vriendelijke groet,");
        col.Item().Text(Leverancier.Naam);
    }
}
