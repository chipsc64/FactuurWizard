namespace FactuurWizard.Core.Model;

public class Adres
{
    public string Straat { get; set; } = "";
    public string Huisnummer { get; set; } = "";

    /// <summary>
    /// Huisletter en/of huisnummertoevoeging, bijv. "A", "2", "A-2" of "hs".
    /// Zie <see cref="HuisnummerVolledig"/> voor de notatie.
    /// </summary>
    public string Toevoeging { get; set; } = "";

    public string Postcode { get; set; } = "";
    public string Plaats { get; set; } = "";
    public string Land { get; set; } = "Nederland";

    public bool IsLeeg =>
        string.IsNullOrWhiteSpace(Straat) && string.IsNullOrWhiteSpace(Postcode) && string.IsNullOrWhiteSpace(Plaats);

    public bool IsNederlands => string.IsNullOrWhiteSpace(Land) || Land.Trim().Equals("Nederland", StringComparison.OrdinalIgnoreCase);

    /// <summary>
    /// Huisnummer met toevoeging zoals gebruikelijk: een losse letter direct erachter ("12A", "12A-2"),
    /// een andere toevoeging met een streepje ("12-2", "12-hs"). Begint de toevoeging zelf met een streepje,
    /// dan wordt die letterlijk overgenomen.
    /// </summary>
    public string HuisnummerVolledig
    {
        get
        {
            var nummer = Huisnummer.Trim();
            var toevoeging = Toevoeging.Trim();
            if (toevoeging.Length == 0)
                return nummer;
            var isHuisletter = char.IsLetter(toevoeging[0]) && (toevoeging.Length == 1 || toevoeging[1] == '-');
            return isHuisletter || toevoeging[0] == '-' ? nummer + toevoeging : $"{nummer}-{toevoeging}";
        }
    }

    public string StraatRegel => $"{Straat.Trim()} {HuisnummerVolledig}".Trim();
    public string PostcodePlaatsRegel => $"{Postcode.Trim()} {Plaats.Trim()}".Trim();

    public Adres Kopie() => (Adres)MemberwiseClone();
}
