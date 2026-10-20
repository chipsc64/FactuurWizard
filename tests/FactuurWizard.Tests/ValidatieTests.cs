using FactuurWizard.Core.Services;

namespace FactuurWizard.Tests;

public class ValidatieTests
{
    [Theory]
    [InlineData("NL91ABNA0417164300", true)]
    [InlineData("nl91 abna 0417 1643 00", true)]
    [InlineData("NL92ABNA0417164300", false)]
    [InlineData("NL91ABNA041716", false)]
    public void Iban(string iban, bool geldig) => Assert.Equal(geldig, Validatie.IsGeldigeIban(iban));

    [Fact]
    public void Iban_wordt_in_groepen_van_vier_geformatteerd() =>
        Assert.Equal("NL91 ABNA 0417 1643 00", Validatie.FormatteerIban("nl91abna0417164300"));

    [Theory]
    [InlineData("NL123456789B01", true)]
    [InlineData("NL 1234.56.789.B01", true)]
    [InlineData("NL123456789", false)]
    [InlineData("BE0123456789", true)]
    public void BtwNummer(string nummer, bool geldig) => Assert.Equal(geldig, Validatie.IsGeldigBtwNummer(nummer));

    [Theory]
    [InlineData("12345678", true)]
    [InlineData("1234567", false)]
    public void KvkNummer(string nummer, bool geldig) => Assert.Equal(geldig, Validatie.IsGeldigKvkNummer(nummer));

    [Theory]
    [InlineData("1234 AB", true)]
    [InlineData("1234ab", true)]
    [InlineData("123 AB", false)]
    public void Postcode(string postcode, bool geldig) => Assert.Equal(geldig, Validatie.IsGeldigePostcode(postcode));

    [Fact]
    public void Postcode_wordt_genormaliseerd() => Assert.Equal("1234 AB", Validatie.FormatteerPostcode("1234ab"));

    [Fact]
    public void Plaatshouders_worden_gevuld_en_onbekende_blijven_staan()
    {
        var tekst = Plaatshouders.Vul("Factuur {Factuurnummer} voor {Onbekend}",
            new Dictionary<string, string> { ["Factuurnummer"] = "2026-0001" });
        Assert.Equal("Factuur 2026-0001 voor {Onbekend}", tekst);
    }
}
