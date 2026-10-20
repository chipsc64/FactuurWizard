using System.Collections.Concurrent;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json.Serialization;
using FactuurWizard.Core.Services;

namespace FactuurWizard.Data.Services;

public sealed record AdresSuggestie(string Straat, string Huisnummer, string Toevoeging, string Postcode, string Plaats);

public enum ZoekStatus
{
    Gevonden,
    NietGevonden,
    /// <summary>Geen antwoord binnen de wachttijd.</summary>
    GeenAntwoord,
    /// <summary>Geen verbinding of een onverwacht antwoord.</summary>
    Mislukt,
}

public sealed record ZoekResultaat(ZoekStatus Status, IReadOnlyList<AdresSuggestie> Adressen);

/// <summary>
/// Zoekt Nederlandse adressen op postcode en huisnummer in de Basisregistratie Adressen en Gebouwen (BAG),
/// via de gratis diensten van PDOK (overheid, geen API-sleutel nodig).
/// <para>
/// De Locatieserver is meestal binnen een fractie van een seconde klaar, maar geeft soms lange tijd geen antwoord.
/// Daarom wordt tegelijk de (tragere) BAG OGC API gevraagd en dient die als terugval.
/// </para>
/// </summary>
public sealed class AdresZoeker(HttpClient http)
{
    public const string LocatieserverUrl = "https://api.pdok.nl/bzk/locatieserver/search/v3_1/free";
    public const string BagUrl = "https://api.pdok.nl/kadaster/bag/ogc/v2/collections/adres/items";

    public static AdresZoeker Standaard { get; } = new(new HttpClient
    {
        Timeout = TimeSpan.FromSeconds(30),
        DefaultRequestHeaders = { { "User-Agent", "FactuurWizard" } },
    });

    public TimeSpan WachttijdLocatieserver { get; init; } = TimeSpan.FromSeconds(3);
    public TimeSpan WachttijdBag { get; init; } = TimeSpan.FromSeconds(12);

    private readonly ConcurrentDictionary<string, ZoekResultaat> _cache = new();

    public async Task<ZoekResultaat> ZoekAsync(string postcode, string huisnummer, CancellationToken annuleren = default)
    {
        var pc = Validatie.Compact(postcode);
        if (!Validatie.IsGeldigePostcode(pc) || !int.TryParse(huisnummer.Trim(), out var nummer) || nummer <= 0)
            return new ZoekResultaat(ZoekStatus.NietGevonden, []);

        var sleutel = $"{pc}|{nummer}";
        if (_cache.TryGetValue(sleutel, out var bekend))
            return bekend;

        using var beide = CancellationTokenSource.CreateLinkedTokenSource(annuleren);
        var snel = ViaLocatieserverAsync(pc, nummer, beide.Token);
        var terugval = ViaBagAsync(pc, nummer, beide.Token);

        var resultaat = await snel;
        if (resultaat.Status is ZoekStatus.GeenAntwoord or ZoekStatus.Mislukt)
        {
            var bag = await terugval;
            resultaat = bag.Status is ZoekStatus.Gevonden or ZoekStatus.NietGevonden ? bag
                : new ZoekResultaat(resultaat.Status == ZoekStatus.GeenAntwoord || bag.Status == ZoekStatus.GeenAntwoord
                    ? ZoekStatus.GeenAntwoord : ZoekStatus.Mislukt, []);
        }
        await beide.CancelAsync();
        annuleren.ThrowIfCancellationRequested();

        // Fouten niet onthouden: een volgende poging kan wel lukken.
        if (resultaat.Status is ZoekStatus.Gevonden or ZoekStatus.NietGevonden)
            _cache[sleutel] = resultaat;
        return resultaat;
    }

    private Task<ZoekResultaat> ViaLocatieserverAsync(string pc, int nummer, CancellationToken token) =>
        HaalAsync<LocatieserverAntwoord>(
            $"{LocatieserverUrl}?q={Uri.EscapeDataString($"postcode:{pc} and huisnummer:{nummer}")}&fq=type:adres" +
            "&fl=straatnaam,huisnummer,huisletter,huisnummertoevoeging,postcode,woonplaatsnaam&rows=100",
            WachttijdLocatieserver, token,
            antwoord => (antwoord.Response?.Docs ?? [])
                .Where(d => d.Huisnummer == nummer)
                .Select(d => Maak(d.Straatnaam, d.Huisnummer, d.Huisletter, d.Huisnummertoevoeging, d.Postcode ?? pc, d.Woonplaatsnaam)));

    /// <summary>
    /// Vraagt alle adressen van de postcode op en filtert zelf op huisnummer: dat is bij deze dienst veel sneller
    /// dan op beide filteren. Ingetrokken adressen worden overgeslagen.
    /// </summary>
    private Task<ZoekResultaat> ViaBagAsync(string pc, int nummer, CancellationToken token) =>
        HaalAsync<BagAntwoord>(
            $"{BagUrl}?f=json&limit=1000&postcode={pc}",
            WachttijdBag, token,
            antwoord => (antwoord.Features ?? [])
                .Select(f => f.Properties)
                .Where(p => p is not null && p.Huisnummer == nummer &&
                            !(p.Status ?? "").Contains("ingetrokken", StringComparison.OrdinalIgnoreCase))
                .Select(p => Maak(p!.OpenbareRuimteNaam, p.Huisnummer, p.Huisletter, p.Toevoeging, p.Postcode ?? pc, p.WoonplaatsNaam)));

    private async Task<ZoekResultaat> HaalAsync<T>(
        string url, TimeSpan wachttijd, CancellationToken token, Func<T, IEnumerable<AdresSuggestie?>> lees)
    {
        using var tijdslimiet = CancellationTokenSource.CreateLinkedTokenSource(token);
        tijdslimiet.CancelAfter(wachttijd);
        try
        {
            using var antwoord = await http.GetAsync(url, tijdslimiet.Token);
            if (antwoord.StatusCode is HttpStatusCode.GatewayTimeout or HttpStatusCode.ServiceUnavailable)
                return new ZoekResultaat(ZoekStatus.GeenAntwoord, []);
            if (!antwoord.IsSuccessStatusCode)
                return new ZoekResultaat(ZoekStatus.Mislukt, []);

            var inhoud = await antwoord.Content.ReadFromJsonAsync<T>(tijdslimiet.Token);
            var adressen = (inhoud is null ? [] : lees(inhoud))
                .OfType<AdresSuggestie>()
                .Distinct()
                .OrderBy(a => a.Toevoeging.Length).ThenBy(a => a.Toevoeging, StringComparer.OrdinalIgnoreCase)
                .ToList();
            return new ZoekResultaat(adressen.Count > 0 ? ZoekStatus.Gevonden : ZoekStatus.NietGevonden, adressen);
        }
        catch (OperationCanceledException) when (!token.IsCancellationRequested)
        {
            return new ZoekResultaat(ZoekStatus.GeenAntwoord, []);
        }
        catch (OperationCanceledException)
        {
            return new ZoekResultaat(ZoekStatus.GeenAntwoord, []); // Afgebroken omdat de andere bron al antwoord gaf.
        }
        catch (Exception ex) when (ex is HttpRequestException or System.Text.Json.JsonException)
        {
            return new ZoekResultaat(ZoekStatus.Mislukt, []);
        }
    }

    private static AdresSuggestie? Maak(string? straat, int huisnummer, string? huisletter, string? toevoeging, string postcode, string? plaats) =>
        string.IsNullOrWhiteSpace(straat) || string.IsNullOrWhiteSpace(plaats)
            ? null
            : new AdresSuggestie(straat.Trim(), huisnummer.ToString(), Toevoeging(huisletter, toevoeging),
                Validatie.FormatteerPostcode(postcode), plaats.Trim());

    /// <summary>
    /// Zet BAG-huisletter en -toevoeging om naar het veld <see cref="Core.Model.Adres.Toevoeging"/>: "A", "A-2" of "2".
    /// </summary>
    public static string Toevoeging(string? huisletter, string? toevoeging)
    {
        var letter = huisletter?.Trim() ?? "";
        var toev = toevoeging?.Trim() ?? "";
        return letter.Length > 0 && toev.Length > 0 ? $"{letter}-{toev}" : letter + toev;
    }

    private sealed class LocatieserverAntwoord
    {
        [JsonPropertyName("response")] public LocatieserverResponse? Response { get; set; }
    }

    private sealed class LocatieserverResponse
    {
        [JsonPropertyName("docs")] public List<LocatieserverDoc>? Docs { get; set; }
    }

    private sealed class LocatieserverDoc
    {
        [JsonPropertyName("straatnaam")] public string? Straatnaam { get; set; }
        [JsonPropertyName("huisnummer")] public int Huisnummer { get; set; }
        [JsonPropertyName("huisletter")] public string? Huisletter { get; set; }
        [JsonPropertyName("huisnummertoevoeging")] public string? Huisnummertoevoeging { get; set; }
        [JsonPropertyName("postcode")] public string? Postcode { get; set; }
        [JsonPropertyName("woonplaatsnaam")] public string? Woonplaatsnaam { get; set; }
    }

    private sealed class BagAntwoord
    {
        [JsonPropertyName("features")] public List<BagFeature>? Features { get; set; }
    }

    private sealed class BagFeature
    {
        [JsonPropertyName("properties")] public BagAdres? Properties { get; set; }
    }

    private sealed class BagAdres
    {
        [JsonPropertyName("openbare_ruimte_naam")] public string? OpenbareRuimteNaam { get; set; }
        [JsonPropertyName("huisnummer")] public int Huisnummer { get; set; }
        [JsonPropertyName("huisletter")] public string? Huisletter { get; set; }
        [JsonPropertyName("toevoeging")] public string? Toevoeging { get; set; }
        [JsonPropertyName("postcode")] public string? Postcode { get; set; }
        [JsonPropertyName("woonplaats_naam")] public string? WoonplaatsNaam { get; set; }
        [JsonPropertyName("status")] public string? Status { get; set; }
    }
}
