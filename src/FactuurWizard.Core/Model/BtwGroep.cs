namespace FactuurWizard.Core.Model;

public class BtwGroep
{
    public int Id { get; set; }
    public string Naam { get; set; } = "";
    public BtwSoort Soort { get; set; }
    public List<BtwTarief> Tarieven { get; set; } = [];
}

/// <summary>
/// Percentage van een btw-groep binnen een geldigheidsperiode.
/// </summary>
public class BtwTarief
{
    public int Id { get; set; }
    public int BtwGroepId { get; set; }
    public decimal Percentage { get; set; }
    public DateOnly Ingangsdatum { get; set; }
    public DateOnly? Einddatum { get; set; }

    public bool GeldigOp(DateOnly datum) =>
        datum >= Ingangsdatum && (Einddatum is not { } eind || datum <= eind);
}
