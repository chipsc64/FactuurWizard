using System.Net;
using System.Text;
using FactuurWizard.Core.Model;
using FactuurWizard.Data.Services;

namespace FactuurWizard.Tests;

public class AdresTests
{
    [Theory]
    [InlineData("12", "", "12")]
    [InlineData("12", "A", "12A")]
    [InlineData("12", "A-2", "12A-2")]
    [InlineData("12", "2", "12-2")]
    [InlineData("12", "hs", "12-hs")]
    [InlineData("1", "-H", "1-H")]
    public void Huisnummer_met_toevoeging(string nummer, string toevoeging, string verwacht) =>
        Assert.Equal(verwacht, new Adres { Huisnummer = nummer, Toevoeging = toevoeging }.HuisnummerVolledig);

    [Theory]
    [InlineData("A", null, "A")]
    [InlineData("A", "2", "A-2")]
    [InlineData(null, "2", "2")]
    [InlineData(null, "H", "H")]
    public void Bag_toevoeging_omzetten(string? letter, string? toevoeging, string verwacht) =>
        Assert.Equal(verwacht, AdresZoeker.Toevoeging(letter, toevoeging));

    private const string LocatieserverJson = """
        {"response":{"numFound":2,"docs":[
          {"straatnaam":"'s-Gravesandeplein","huisnummer":1,"huisnummertoevoeging":"2","postcode":"1091BB","woonplaatsnaam":"Amsterdam"},
          {"straatnaam":"'s-Gravesandeplein","huisnummer":1,"huisnummertoevoeging":"H","postcode":"1091BB","woonplaatsnaam":"Amsterdam"}]}}
        """;

    private const string BagJson = """
        {"features":[
          {"properties":{"openbare_ruimte_naam":"'s-Gravesandeplein","huisnummer":1,"huisletter":"","toevoeging":"2","postcode":"1091BB","woonplaats_naam":"Amsterdam","status":"Naamgeving uitgegeven"}},
          {"properties":{"openbare_ruimte_naam":"'s-Gravesandeplein","huisnummer":1,"toevoeging":"9","postcode":"1091BB","woonplaats_naam":"Amsterdam","status":"Naamgeving ingetrokken"}},
          {"properties":{"openbare_ruimte_naam":"'s-Gravesandeplein","huisnummer":3,"toevoeging":"1","postcode":"1091BB","woonplaats_naam":"Amsterdam","status":"Naamgeving uitgegeven"}}]}
        """;

    /// <summary>Geeft per bron een vast antwoord, of laat de bron hangen (null).</summary>
    private sealed class Bronnen(string? locatieserver, string? bag, HttpStatusCode code = HttpStatusCode.OK) : HttpMessageHandler
    {
        public int Aanroepen;

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken token)
        {
            Interlocked.Increment(ref Aanroepen);
            var inhoud = request.RequestUri!.AbsoluteUri.StartsWith(AdresZoeker.LocatieserverUrl) ? locatieserver : bag;
            if (inhoud is null)
                await Task.Delay(Timeout.Infinite, token);
            return new HttpResponseMessage(code) { Content = new StringContent(inhoud ?? "", Encoding.UTF8, "application/json") };
        }
    }

    private static AdresZoeker Zoeker(HttpMessageHandler handler) => new(new HttpClient(handler))
    {
        WachttijdLocatieserver = TimeSpan.FromMilliseconds(200),
        WachttijdBag = TimeSpan.FromMilliseconds(400),
    };

    [Fact]
    public async Task Locatieserver_wordt_omgezet_en_onthouden()
    {
        var bronnen = new Bronnen(LocatieserverJson, BagJson);
        var zoeker = Zoeker(bronnen);

        var resultaat = await zoeker.ZoekAsync("1091 bb", "1");
        var aanroepen = bronnen.Aanroepen;
        await zoeker.ZoekAsync("1091BB", " 1 ");

        Assert.Equal(ZoekStatus.Gevonden, resultaat.Status);
        Assert.Equal(["2", "H"], resultaat.Adressen.Select(a => a.Toevoeging));
        Assert.All(resultaat.Adressen, a => Assert.Equal("1091 BB", a.Postcode));
        Assert.Equal(aanroepen, bronnen.Aanroepen);
    }

    [Fact]
    public async Task Hangende_locatieserver_valt_terug_op_bag_zonder_ingetrokken_adressen()
    {
        var resultaat = await Zoeker(new Bronnen(null, BagJson)).ZoekAsync("1091BB", "1");

        Assert.Equal(ZoekStatus.Gevonden, resultaat.Status);
        Assert.Equal(["2"], resultaat.Adressen.Select(a => a.Toevoeging));
    }

    [Fact]
    public async Task Beide_bronnen_hangen_geeft_geen_antwoord()
    {
        Assert.Equal(ZoekStatus.GeenAntwoord, (await Zoeker(new Bronnen(null, null)).ZoekAsync("1234AB", "1")).Status);
    }

    [Fact]
    public async Task Gateway_timeout_valt_terug_en_wordt_niet_onthouden()
    {
        var bronnen = new Bronnen("", "", HttpStatusCode.GatewayTimeout);
        var zoeker = Zoeker(bronnen);

        Assert.Equal(ZoekStatus.GeenAntwoord, (await zoeker.ZoekAsync("1234AB", "1")).Status);
        await zoeker.ZoekAsync("1234AB", "1");
        Assert.Equal(4, bronnen.Aanroepen);
    }

    [Fact]
    public async Task Ongeldige_invoer_vraagt_niets_op()
    {
        var bronnen = new Bronnen("{}", "{}");
        var zoeker = Zoeker(bronnen);
        Assert.Equal(ZoekStatus.NietGevonden, (await zoeker.ZoekAsync("123", "1")).Status);
        Assert.Equal(ZoekStatus.NietGevonden, (await zoeker.ZoekAsync("1234AB", "abc")).Status);
        Assert.Equal(0, bronnen.Aanroepen);
    }
}
