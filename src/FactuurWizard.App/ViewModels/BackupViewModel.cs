using System.Diagnostics;
using System.IO;
using System.Windows;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using FactuurWizard.App.Services;
using FactuurWizard.Data;
using FactuurWizard.Data.Services;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace FactuurWizard.App.ViewModels;

public partial class BackupViewModel(
    IDbContextFactory<FactuurWizardDbContext> dbFactory,
    Opslaglocatie opslag,
    DialoogService dialogen,
    Updater updater) : PaginaViewModel(dbFactory)
{
    public override string Titel => "Backup";

    public string DataMap => opslag.DataMap;

    [ObservableProperty]
    public partial string LaatsteBackup { get; set; } = "";

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(MaakBackupCommand), nameof(TerugzettenCommand))]
    public partial bool IsBezig { get; set; }

    private bool IsNietBezig() => !IsBezig;

    public override Task LaadAsync()
    {
        var map = Backup.StandaardMap(opslag);
        var laatste = Directory.Exists(map)
            ? new DirectoryInfo(map).GetFiles("*.zip").OrderByDescending(f => f.LastWriteTime).FirstOrDefault()
            : null;
        LaatsteBackup = laatste is null
            ? "Er staat nog geen backup in de standaardmap."
            : $"Laatste backup in de standaardmap: {laatste.LastWriteTime:d-M-yyyy HH:mm} ({laatste.Name}).";
        return Task.CompletedTask;
    }

    [RelayCommand(CanExecute = nameof(IsNietBezig))]
    private async Task MaakBackupAsync()
    {
        var map = Backup.StandaardMap(opslag);
        Directory.CreateDirectory(map);
        var pad = dialogen.KiesOpslaanAls("Backup opslaan", "Backup (*.zip)|*.zip",
            Backup.StandaardBestandsnaam(DateTime.Now), map);
        if (pad is null)
            return;

        try
        {
            IsBezig = true;
            await Task.Run(() => Backup.Maak(opslag, pad, updater.HuidigeVersie));
            await LaadAsync();
            ToonSucces($"De backup is opgeslagen als {pad}");
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or SqliteException)
        {
            Foutlog.Schrijf(ex);
            ToonFouten([$"De backup kon niet worden gemaakt: {ex.Message}"]);
        }
        finally
        {
            IsBezig = false;
        }
    }

    [RelayCommand(CanExecute = nameof(IsNietBezig))]
    private async Task TerugzettenAsync()
    {
        var pad = dialogen.KiesBestand("Kies de backup om terug te zetten", "Backup (*.zip)|*.zip");
        if (pad is null)
            return;

        BackupInfo info;
        try
        {
            info = Backup.Controleer(pad);
        }
        catch (InvalidDataException ex)
        {
            ToonFouten([ex.Message]);
            return;
        }

        if (!dialogen.Bevestig(
                $"Backup van {info.Gemaakt:d-M-yyyy HH:mm} (versie {info.Versie}).\n\n" +
                "Al uw huidige gegevens (klanten, facturen, instellingen en PDF's) worden vervangen door die uit deze backup. " +
                "Van de huidige gegevens wordt eerst automatisch een backup gemaakt in de map Backups.\n\n" +
                "FactuurWizard start daarna opnieuw. Doorgaan?", "Backup terugzetten"))
            return;

        try
        {
            IsBezig = true;
            var veiligheid = Path.Combine(Backup.StandaardMap(opslag),
                $"Voor terugzetten {DateTime.Now:yyyy-MM-dd HHmmss}.zip");
            await Task.Run(() => Backup.Maak(opslag, veiligheid, updater.HuidigeVersie));

            // Alle verbindingen sluiten voordat de database wordt vervangen.
            Dispose();
            await Task.Run(() => Backup.TerugZetten(opslag, pad));
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or InvalidDataException or SqliteException)
        {
            Foutlog.Schrijf(ex);
            dialogen.ToonFout($"Het terugzetten is niet gelukt: {ex.Message}\n\nFactuurWizard wordt opnieuw gestart.");
        }

        Process.Start(Environment.ProcessPath!);
        Application.Current.Shutdown();
    }

    [RelayCommand]
    private void OpenMap()
    {
        var map = Backup.StandaardMap(opslag);
        Directory.CreateDirectory(map);
        Process.Start("explorer.exe", $"\"{map}\"");
    }
}
