using System.IO.Compression;
using System.Text.Json;
using Microsoft.Data.Sqlite;

namespace FactuurWizard.Data.Services;

public sealed record BackupInfo(DateTime Gemaakt, string Versie, int AantalBestanden);

/// <summary>
/// Backup van alle gegevens in één zip-bestand: de database en alle bestanden in de datamap
/// (factuur-PDF's, herinneringen, btw-overzichten). De map met backups zelf gaat niet mee.
/// </summary>
public static class Backup
{
    private const string DatabaseInZip = "factuurwizard.db";
    private const string InfoInZip = "backup.json";
    private const string BackupMap = "Backups";

    public static void Maak(Opslaglocatie opslag, string zipPad, string versie)
    {
        var tijdelijk = Path.Combine(Path.GetTempPath(), $"FactuurWizard-backup-{Guid.NewGuid():N}");
        Directory.CreateDirectory(tijdelijk);
        try
        {
            // De SQLite-backupfunctie geeft een consistente kopie, ook als de database in gebruik is.
            var databaseKopie = Path.Combine(tijdelijk, DatabaseInZip);
            using (var bron = new SqliteConnection(opslag.ConnectionString))
            using (var doel = new SqliteConnection($"Data Source={databaseKopie};Pooling=False"))
            {
                bron.Open();
                doel.Open();
                bron.BackupDatabase(doel);
            }

            var bestanden = Directory.GetFiles(opslag.DataMap, "*", SearchOption.AllDirectories)
                .Where(b => !HoortNietInBackup(opslag.DataMap, b))
                .ToList();

            // Eerst naar een tijdelijk zip-bestand schrijven, zodat een afgebroken backup geen half bestand achterlaat.
            var tijdelijkeZip = Path.Combine(tijdelijk, "backup.zip");
            using (var zip = ZipFile.Open(tijdelijkeZip, ZipArchiveMode.Create))
            {
                zip.CreateEntryFromFile(databaseKopie, DatabaseInZip, CompressionLevel.Optimal);
                foreach (var bestand in bestanden)
                    zip.CreateEntryFromFile(bestand, ZipNaam(opslag.DataMap, bestand), CompressionLevel.Optimal);

                var info = zip.CreateEntry(InfoInZip);
                using var schrijver = info.Open();
                JsonSerializer.Serialize(schrijver, new BackupInfo(DateTime.Now, versie, bestanden.Count + 1));
            }

            Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(zipPad))!);
            File.Move(tijdelijkeZip, zipPad, overwrite: true);
        }
        finally
        {
            Directory.Delete(tijdelijk, recursive: true);
        }
    }

    /// <summary>Controleert of het bestand een FactuurWizard-backup is en geeft de gegevens ervan.</summary>
    public static BackupInfo Controleer(string zipPad)
    {
        ZipArchive zip;
        try
        {
            zip = ZipFile.OpenRead(zipPad);
        }
        catch (InvalidDataException ex)
        {
            throw new InvalidDataException("Dit bestand is geen geldig zip-bestand.", ex);
        }

        using (zip)
        try
        {
            if (zip.GetEntry(DatabaseInZip) is null)
                throw new InvalidDataException("Dit bestand is geen FactuurWizard-backup.");
            if (zip.GetEntry(InfoInZip) is not { } infoEntry)
                return new BackupInfo(File.GetLastWriteTime(zipPad), "onbekend", zip.Entries.Count);
            using var stroom = infoEntry.Open();
            return JsonSerializer.Deserialize<BackupInfo>(stroom)
                   ?? throw new InvalidDataException("De backup-informatie ontbreekt.");
        }
        catch (Exception ex) when (ex is JsonException or IOException)
        {
            throw new InvalidDataException("Dit bestand is geen geldige FactuurWizard-backup.", ex);
        }
    }

    /// <summary>
    /// Vervangt alle gegevens door die uit de backup. Alle databaseverbindingen moeten gesloten zijn;
    /// de applicatie moet daarna opnieuw starten.
    /// </summary>
    public static void TerugZetten(Opslaglocatie opslag, string zipPad)
    {
        Controleer(zipPad);
        SqliteConnection.ClearAllPools();

        var uitgepakt = Path.Combine(Path.GetTempPath(), $"FactuurWizard-terugzetten-{Guid.NewGuid():N}");
        try
        {
            // ExtractToDirectory weigert paden buiten de doelmap ("zip slip").
            ZipFile.ExtractToDirectory(zipPad, uitgepakt);
            File.Delete(Path.Combine(uitgepakt, InfoInZip));
            ControleerDatabase(Path.Combine(uitgepakt, DatabaseInZip));

            // Huidige gegevens verwijderen, behalve de map met backups.
            foreach (var bestand in Directory.GetFiles(opslag.DataMap))
                File.Delete(bestand);
            foreach (var map in Directory.GetDirectories(opslag.DataMap)
                         .Where(m => !Path.GetFileName(m).Equals(BackupMap, StringComparison.OrdinalIgnoreCase)))
                Directory.Delete(map, recursive: true);

            foreach (var bestand in Directory.GetFiles(uitgepakt, "*", SearchOption.AllDirectories))
            {
                var doel = Path.Combine(opslag.DataMap, Path.GetRelativePath(uitgepakt, bestand));
                Directory.CreateDirectory(Path.GetDirectoryName(doel)!);
                File.Move(bestand, doel, overwrite: true);
            }
        }
        finally
        {
            if (Directory.Exists(uitgepakt))
                Directory.Delete(uitgepakt, recursive: true);
        }
    }

    public static string StandaardBestandsnaam(DateTime moment) => $"FactuurWizard backup {moment:yyyy-MM-dd HHmm}.zip";

    public static string StandaardMap(Opslaglocatie opslag) => Path.Combine(opslag.DataMap, BackupMap);

    private static void ControleerDatabase(string pad)
    {
        using var verbinding = new SqliteConnection($"Data Source={pad};Mode=ReadOnly;Pooling=False");
        verbinding.Open();
        using var opdracht = verbinding.CreateCommand();
        opdracht.CommandText = "SELECT count(*) FROM sqlite_master WHERE type = 'table' AND name IN ('Factuur', 'Klant')";
        if (Convert.ToInt32(opdracht.ExecuteScalar()) != 2)
            throw new InvalidDataException("De database in de backup is niet van FactuurWizard.");
    }

    private static bool HoortNietInBackup(string dataMap, string bestand)
    {
        var relatief = Path.GetRelativePath(dataMap, bestand);
        return relatief.StartsWith(BackupMap + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase) ||
               Path.GetFileName(relatief).StartsWith(Opslaglocatie.DatabaseBestandsnaam, StringComparison.OrdinalIgnoreCase);
    }

    private static string ZipNaam(string dataMap, string bestand) =>
        Path.GetRelativePath(dataMap, bestand).Replace(Path.DirectorySeparatorChar, '/');
}
