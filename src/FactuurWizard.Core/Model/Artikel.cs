namespace FactuurWizard.Core.Model;

/// <summary>
/// Product of dienst met een vaste prijs (exclusief btw).
/// </summary>
public class Artikel
{
    public int Id { get; set; }
    public ArtikelSoort Soort { get; set; }
    public string Artikelnummer { get; set; } = "";
    public string Omschrijving { get; set; } = "";
    public string Eenheid { get; set; } = "stuk";
    public decimal PrijsExclBtw { get; set; }
    public int BtwGroepId { get; set; }
    public BtwGroep? BtwGroep { get; set; }
    public bool Gearchiveerd { get; set; }
}
