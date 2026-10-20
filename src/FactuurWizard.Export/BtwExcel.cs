using ClosedXML.Excel;
using FactuurWizard.Core.Model;
using FactuurWizard.Core.Services;

namespace FactuurWizard.Export;

/// <summary>
/// Exporteert het btw-overzicht naar Excel voor de boekhouder: een blad met de aangifterubrieken
/// en een blad met de specificatie per factuur.
/// </summary>
public static class BtwExcel
{
    private const string Euro = "€ #,##0.00;€ -#,##0.00";
    private const string Datum = "dd-mm-yyyy";

    public static void Exporteer(BtwOverzicht overzicht, string bedrijfsnaam, string periodeNaam, Stream doel)
    {
        using var werkmap = new XLWorkbook();
        Overzicht(werkmap.AddWorksheet("Overzicht"), overzicht, bedrijfsnaam, periodeNaam);
        Specificatie(werkmap.AddWorksheet("Specificatie"), overzicht);
        werkmap.SaveAs(doel);
    }

    private static void Overzicht(IXLWorksheet blad, BtwOverzicht o, string bedrijfsnaam, string periodeNaam)
    {
        blad.Cell("A1").Value = $"Btw-overzicht {periodeNaam}";
        blad.Cell("A1").Style.Font.SetBold().Font.SetFontSize(14);
        blad.Cell("A2").Value = bedrijfsnaam;
        blad.Cell("A3").Value = $"Periode {o.Van:dd-MM-yyyy} t/m {o.Tot:dd-MM-yyyy}, factuurstelsel (op factuurdatum)";

        var rij = 5;
        Kop(blad, rij, "Rubriek", "Omschrijving", "Omzet excl. btw", "Btw", "Omzet (hele euro's)", "Btw (hele euro's)");
        foreach (var r in o.Rubrieken)
        {
            rij++;
            blad.Cell(rij, 1).Value = r.Code;
            blad.Cell(rij, 2).Value = r.Omschrijving;
            blad.Cell(rij, 3).Value = r.Omzet;
            blad.Cell(rij, 4).Value = r.Btw;
            blad.Cell(rij, 5).Value = BtwRapportage.HeleEuros(r.Omzet);
            blad.Cell(rij, 6).Value = BtwRapportage.HeleEuros(r.Btw);
        }

        rij++;
        blad.Cell(rij, 1).Value = "5a";
        blad.Cell(rij, 2).Value = BtwRapportage.Omschrijving5a;
        blad.Cell(rij, 4).Value = o.VerschuldigdeBtw;
        // In de aangifte is 5a de som van de (afgeronde) regels, niet het afgeronde totaal.
        blad.Cell(rij, 6).Value = o.Rubrieken.Sum(r => BtwRapportage.HeleEuros(r.Btw));
        blad.Range(rij, 1, rij, 6).Style.Font.SetBold().Border.SetTopBorder(XLBorderStyleValues.Thin);

        blad.Range(6, 3, rij, 4).Style.NumberFormat.Format = Euro;
        blad.Range(6, 5, rij, 6).Style.NumberFormat.Format = "€ #,##0;€ -#,##0";

        rij += 2;
        if (o.OmzetKor != 0)
        {
            blad.Cell(rij, 1).Value = "Omzet onder de kleineondernemersregeling (niet in de aangifte):";
            blad.Cell(rij, 4).Value = o.OmzetKor;
            blad.Cell(rij, 4).Style.NumberFormat.Format = Euro;
            rij += 2;
        }

        blad.Cell(rij, 1).Value = $"Aantal facturen en creditfacturen: {o.Facturen.Count}";
        blad.Cell(rij + 1, 1).Value = "Let op: voorbelasting (rubriek 5b) komt uit uw inkoopfacturen en is hier niet opgenomen.";
        blad.Cell(rij + 2, 1).Value = "In de aangifte vult u hele euro's in; naar beneden afronden is in uw voordeel.";
        blad.Range(rij, 1, rij + 2, 1).Style.Font.SetItalic();

        blad.Column(1).Width = 10;
        blad.Column(2).Width = 55;
        blad.Columns(3, 6).Width = 20;
    }

    private static void Specificatie(IXLWorksheet blad, BtwOverzicht o)
    {
        Kop(blad, 1, "Factuurnummer", "Datum", "Soort", "Klant", "Btw-nummer klant",
            "Omzet 1a (hoog)", "Btw 1a", "Omzet 1b (laag)", "Btw 1b", "Omzet 1e (0%)", "Omzet KOR",
            "Totaal excl. btw", "Totaal btw", "Totaal incl. btw", "Betaald op");

        var rij = 1;
        foreach (var r in o.Facturen)
        {
            rij++;
            var f = r.Factuur;
            blad.Cell(rij, 1).Value = f.Factuurnummer;
            blad.Cell(rij, 2).Value = f.Factuurdatum.ToDateTime(TimeOnly.MinValue);
            blad.Cell(rij, 3).Value = f.Soort == FactuurSoort.Creditfactuur ? "Creditfactuur" : "Factuur";
            blad.Cell(rij, 4).Value = f.KlantGegevens.Naam;
            blad.Cell(rij, 5).Value = f.KlantGegevens.BtwNummer;
            blad.Cell(rij, 6).Value = r.Omzet1a;
            blad.Cell(rij, 7).Value = r.Btw1a;
            blad.Cell(rij, 8).Value = r.Omzet1b;
            blad.Cell(rij, 9).Value = r.Btw1b;
            blad.Cell(rij, 10).Value = r.Omzet1e;
            blad.Cell(rij, 11).Value = r.OmzetKor;
            blad.Cell(rij, 12).Value = r.TotaalExclBtw;
            blad.Cell(rij, 13).Value = r.TotaalBtw;
            blad.Cell(rij, 14).Value = r.TotaalInclBtw;
            if (f.BetaaldOp is { } betaald)
                blad.Cell(rij, 15).Value = betaald.ToDateTime(TimeOnly.MinValue);
        }

        // Totaalregel met formules, zodat de boekhouder ze kan narekenen.
        var totaalRij = rij + 1;
        blad.Cell(totaalRij, 1).Value = "Totaal";
        for (var kolom = 6; kolom <= 14; kolom++)
            blad.Cell(totaalRij, kolom).FormulaA1 = rij >= 2
                ? $"SUM({blad.Cell(2, kolom).Address}:{blad.Cell(rij, kolom).Address})"
                : "0";
        blad.Range(totaalRij, 1, totaalRij, 15).Style.Font.SetBold().Border.SetTopBorder(XLBorderStyleValues.Thin);

        blad.Range(2, 2, totaalRij, 2).Style.NumberFormat.Format = Datum;
        blad.Range(2, 15, totaalRij, 15).Style.NumberFormat.Format = Datum;
        blad.Range(2, 6, totaalRij, 14).Style.NumberFormat.Format = Euro;

        blad.SheetView.FreezeRows(1);
        if (rij >= 2)
            blad.Range(1, 1, rij, 15).SetAutoFilter();
        blad.Columns(1, 15).AdjustToContents();
    }

    private static void Kop(IXLWorksheet blad, int rij, params string[] koppen)
    {
        for (var i = 0; i < koppen.Length; i++)
            blad.Cell(rij, i + 1).Value = koppen[i];
        var bereik = blad.Range(rij, 1, rij, koppen.Length);
        bereik.Style.Font.SetBold().Fill.SetBackgroundColor(XLColor.FromHtml("#E3ECF5"))
            .Border.SetBottomBorder(XLBorderStyleValues.Thin);
    }
}
