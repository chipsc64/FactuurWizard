namespace FactuurWizard.Core.Model;

/// <summary>
/// Hoogste uitgegeven volgnummer per jaar. De factuurnummering telt per jaar door.
/// </summary>
public class Nummerreeks
{
    public int Jaar { get; set; }
    public int LaatsteVolgnummer { get; set; }
}

/// <summary>
/// Briefpapier-sjabloon. Nu wordt alleen het standaardsjabloon gebruikt;
/// het model ondersteunt meerdere sjablonen voor later.
/// </summary>
public class Briefpapier
{
    public int Id { get; set; }
    public string Naam { get; set; } = "Standaard";
    public bool IsStandaard { get; set; }

    public byte[]? Logo { get; set; }
    public double LogoBreedteMm { get; set; } = 50;

    /// <summary>Bestaand briefpapier als achtergrond (afbeelding of PDF).</summary>
    public AchtergrondSoort AchtergrondSoort { get; set; }
    public byte[]? Achtergrond { get; set; }

    public string Lettertype { get; set; } = "Segoe UI";
    public string Accentkleur { get; set; } = "#1F4E79";
    public string Tekstkleur { get; set; } = "#000000";

    /// <summary>Of de bedrijfsgegevens op de factuur worden afgedrukt (uit bij voorbedrukt briefpapier).</summary>
    public bool ToonBedrijfsgegevens { get; set; } = true;
    public bool BedrijfsgegevensRechts { get; set; } = true;

    /// <summary>Marges in mm, zodat de inhoud binnen voorbedrukt briefpapier valt.</summary>
    public double MargeBovenMm { get; set; } = 20;
    public double MargeOnderMm { get; set; } = 20;
    public double MargeLinksMm { get; set; } = 20;
    public double MargeRechtsMm { get; set; } = 20;

    public string Voettekst { get; set; } = "";
}

/// <summary>
/// Vaste tekst uit de inrichting, met ondersteuning van plaatshouders zoals {Factuurnummer}.
/// </summary>
public class Tekstsjabloon
{
    public int Id { get; set; }
    public TekstSoort Soort { get; set; }
    public string Taal { get; set; } = "nl-NL";
    public string Tekst { get; set; } = "";
}
