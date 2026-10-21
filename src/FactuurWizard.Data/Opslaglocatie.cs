using System.Text.Json;

namespace FactuurWizard.Data;

/// <summary>
/// Bepaalt waar de database en PDF's staan. De gekozen map wordt buiten de database bewaard
/// (in %LocalAppData%\FactuurWizard\opslag.json), omdat de database zelf in die map staat.
/// </summary>
public class Opslaglocatie
{
    public const string DatabaseBestandsnaam = "factuurwizard.db";

    /// <summary>
    /// In %AppData% (niet %LocalAppData%): daar beheert het installatieprogramma de programmamap en
    /// verwijdert die bij het verwijderen van FactuurWizard.
    /// </summary>
    public static string InstellingenMap { get; } = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "FactuurWizard");

    private static readonly string InstellingenBestand = Path.Combine(InstellingenMap, "opslag.json");

    /// <summary>Locatie uit eerdere versies; wordt nog gelezen als er geen nieuwe instelling is.</summary>
    private static readonly string OudInstellingenBestand = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "FactuurWizard", "opslag.json");

    public static string StandaardDataMap => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "FactuurWizard");

    public string DataMap { get; }

    public Opslaglocatie(string dataMap)
    {
        DataMap = dataMap;
    }

    public string DatabasePad => Path.Combine(DataMap, DatabaseBestandsnaam);
    public string ConnectionString => $"Data Source={DatabasePad}";

    public string FactuurMap(int jaar) => Path.Combine(DataMap, "Facturen", jaar.ToString());
    public string BackupMap => Path.Combine(DataMap, "Backups");

    public static Opslaglocatie Laad()
    {
        // Voor tests en ontwikkeling: een afwijkende map via de omgeving, zonder de instelling te wijzigen.
        if (Environment.GetEnvironmentVariable("FACTUURWIZARD_DATAMAP") is { Length: > 0 } overschreven)
            return new Opslaglocatie(overschreven);

        foreach (var bestand in new[] { InstellingenBestand, OudInstellingenBestand })
        {
            try
            {
                if (!File.Exists(bestand))
                    continue;
                var instellingen = JsonSerializer.Deserialize<Instellingen>(File.ReadAllText(bestand));
                if (!string.IsNullOrWhiteSpace(instellingen?.DataMap))
                    return new Opslaglocatie(instellingen.DataMap);
            }
            catch (JsonException)
            {
                // Beschadigd instellingenbestand: probeer de volgende, anders de standaardlocatie.
            }
        }
        return new Opslaglocatie(StandaardDataMap);
    }

    public static bool BevatDatabase(string map) => File.Exists(Path.Combine(map, DatabaseBestandsnaam));

    /// <summary>
    /// Kopieert de database en alle bestanden naar een nieuwe map. De oude map blijft ongewijzigd staan.
    /// Alle databaseverbindingen moeten gesloten zijn.
    /// </summary>
    public void KopieerNaar(string doelMap)
    {
        var bron = Path.GetFullPath(DataMap).TrimEnd('\\') + '\\';
        var doelVolledig = Path.GetFullPath(doelMap).TrimEnd('\\') + '\\';
        if (doelVolledig.StartsWith(bron, StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("De nieuwe map mag niet gelijk zijn aan of binnen de huidige map liggen.");

        Microsoft.Data.Sqlite.SqliteConnection.ClearAllPools();
        Directory.CreateDirectory(doelMap);
        foreach (var bestand in Directory.GetFiles(DataMap, "*", SearchOption.AllDirectories))
        {
            var doel = Path.Combine(doelMap, Path.GetRelativePath(DataMap, bestand));
            Directory.CreateDirectory(Path.GetDirectoryName(doel)!);
            File.Copy(bestand, doel, overwrite: false);
        }
    }

    public static void Bewaar(string dataMap)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(InstellingenBestand)!);
        File.WriteAllText(InstellingenBestand, JsonSerializer.Serialize(new Instellingen { DataMap = dataMap }));
    }

    private sealed class Instellingen
    {
        public string? DataMap { get; set; }
    }
}
