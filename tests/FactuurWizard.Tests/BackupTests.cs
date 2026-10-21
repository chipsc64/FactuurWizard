using System.IO.Compression;
using FactuurWizard.Core.Model;
using FactuurWizard.Data;
using FactuurWizard.Data.Services;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace FactuurWizard.Tests;

public sealed class BackupTests : IDisposable
{
    private readonly string _map = Path.Combine(Path.GetTempPath(), "FactuurWizardBackupTest-" + Guid.NewGuid().ToString("N"));
    private readonly Opslaglocatie _opslag;

    public BackupTests()
    {
        _opslag = new Opslaglocatie(Path.Combine(_map, "data"));
        Directory.CreateDirectory(_opslag.DataMap);
        using var db = NieuweContext();
        DatabaseInitialisatie.Initialiseer(db);
        db.Klanten.Add(new Klant { Klantnummer = 1001, Naam = "Origineel" });
        db.SaveChanges();

        Directory.CreateDirectory(_opslag.FactuurMap(2026));
        File.WriteAllText(Path.Combine(_opslag.FactuurMap(2026), "2026-0001 Origineel.pdf"), "pdf");
    }

    private FactuurWizardDbContext NieuweContext() =>
        new(new DbContextOptionsBuilder<FactuurWizardDbContext>().UseSqlite(_opslag.ConnectionString).Options);

    public void Dispose()
    {
        SqliteConnection.ClearAllPools();
        Directory.Delete(_map, recursive: true);
    }

    [Fact]
    public void Backup_en_terugzetten_herstelt_database_en_bestanden()
    {
        var zip = Path.Combine(_map, "backup.zip");
        Backup.Maak(_opslag, zip, "1.0.0");

        // Na de backup: gegevens wijzigen en een bestand toevoegen.
        using (var db = NieuweContext())
        {
            db.Klanten.Single().Naam = "Gewijzigd";
            db.SaveChanges();
        }
        File.WriteAllText(Path.Combine(_opslag.FactuurMap(2026), "2026-0002 Later.pdf"), "pdf");

        Backup.TerugZetten(_opslag, zip);

        using (var db = NieuweContext())
            Assert.Equal("Origineel", db.Klanten.Single().Naam);
        Assert.True(File.Exists(Path.Combine(_opslag.FactuurMap(2026), "2026-0001 Origineel.pdf")));
        Assert.False(File.Exists(Path.Combine(_opslag.FactuurMap(2026), "2026-0002 Later.pdf")));
    }

    [Fact]
    public void Backupmap_zelf_gaat_niet_mee_en_blijft_bij_terugzetten_staan()
    {
        var eerdere = Path.Combine(Backup.StandaardMap(_opslag), "eerder.zip");
        Directory.CreateDirectory(Backup.StandaardMap(_opslag));
        File.WriteAllText(eerdere, "x");
        var zip = Path.Combine(Backup.StandaardMap(_opslag), "nieuw.zip");

        Backup.Maak(_opslag, zip, "1.0.0");
        using (var archief = ZipFile.OpenRead(zip))
            Assert.DoesNotContain(archief.Entries, e => e.FullName.StartsWith("Backups"));

        Backup.TerugZetten(_opslag, zip);
        Assert.True(File.Exists(eerdere));
    }

    [Fact]
    public void Ander_zipbestand_wordt_geweigerd()
    {
        var zip = Path.Combine(_map, "ander.zip");
        using (var archief = ZipFile.Open(zip, ZipArchiveMode.Create))
            archief.CreateEntry("iets.txt");

        Assert.Throws<InvalidDataException>(() => Backup.Controleer(zip));
        Assert.Throws<InvalidDataException>(() => Backup.TerugZetten(_opslag, zip));
    }

    [Fact]
    public void Backupinformatie_is_leesbaar()
    {
        var zip = Path.Combine(_map, "backup.zip");
        Backup.Maak(_opslag, zip, "1.2.3");

        var info = Backup.Controleer(zip);

        Assert.Equal("1.2.3", info.Versie);
        Assert.Equal(2, info.AantalBestanden);
    }
}
