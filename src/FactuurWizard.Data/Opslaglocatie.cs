using System.Text.Json;

namespace FactuurWizard.Data;

/// <summary>
/// Bepaalt waar de database en PDF's staan. De gekozen map wordt buiten de database bewaard
/// (in %LocalAppData%\FactuurWizard\opslag.json), omdat de database zelf in die map staat.
/// </summary>
public class Opslaglocatie
{
    public const string DatabaseBestandsnaam = "factuurwizard.db";

    private static readonly string InstellingenBestand = Path.Combine(
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
        try
        {
            if (File.Exists(InstellingenBestand))
            {
                var instellingen = JsonSerializer.Deserialize<Instellingen>(File.ReadAllText(InstellingenBestand));
                if (!string.IsNullOrWhiteSpace(instellingen?.DataMap))
                    return new Opslaglocatie(instellingen.DataMap);
            }
        }
        catch (JsonException)
        {
            // Beschadigd instellingenbestand: val terug op de standaardlocatie.
        }
        return new Opslaglocatie(StandaardDataMap);
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
