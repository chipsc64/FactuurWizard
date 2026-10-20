namespace FactuurWizard.Core.Model;

public class Adres
{
    public string Straat { get; set; } = "";
    public string Huisnummer { get; set; } = "";
    public string Postcode { get; set; } = "";
    public string Plaats { get; set; } = "";
    public string Land { get; set; } = "Nederland";

    public bool IsLeeg =>
        string.IsNullOrWhiteSpace(Straat) && string.IsNullOrWhiteSpace(Postcode) && string.IsNullOrWhiteSpace(Plaats);

    public Adres Kopie() => (Adres)MemberwiseClone();
}
