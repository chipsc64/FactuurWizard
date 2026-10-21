using System.Reflection;
using CommunityToolkit.Mvvm.ComponentModel;
using Velopack;
using Velopack.Sources;

namespace FactuurWizard.App.Services;

/// <summary>
/// Controleert via GitHub Releases op een nieuwe versie en installeert die op verzoek van de gebruiker.
/// Werkt alleen in een geïnstalleerde versie, niet tijdens ontwikkeling.
/// </summary>
public partial class Updater : ObservableObject
{
    public const string Repository = "https://github.com/chipsc64/FactuurWizard";

    private readonly UpdateManager _manager = new(new GithubSource(Repository, accessToken: null, prerelease: false));
    private UpdateInfo? _update;

    [ObservableProperty]
    public partial string? NieuweVersie { get; set; }

    [ObservableProperty]
    public partial int? Voortgang { get; set; }

    public bool IsGeinstalleerd => _manager.IsInstalled;

    public string HuidigeVersie => _manager.IsInstalled
        ? _manager.CurrentVersion!.ToString()
        : Assembly.GetExecutingAssembly().GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion
              .Split('+')[0] ?? "onbekend";

    public async Task ControleerAsync()
    {
        if (!_manager.IsInstalled)
            return;
        try
        {
            _update = await _manager.CheckForUpdatesAsync();
            NieuweVersie = _update?.TargetFullRelease.Version.ToString();
        }
        catch (Exception ex)
        {
            // Geen internet of GitHub onbereikbaar: niet storen, volgende keer opnieuw.
            Foutlog.Schrijf(ex);
        }
    }

    /// <summary>Downloadt de nieuwe versie, sluit het programma, installeert en start opnieuw.</summary>
    public async Task BijwerkenAsync()
    {
        if (_update is null)
            return;
        Voortgang = 0;
        try
        {
            await _manager.DownloadUpdatesAsync(_update, p => Voortgang = p);
            _manager.ApplyUpdatesAndRestart(_update);
        }
        finally
        {
            Voortgang = null;
        }
    }
}
