using System.Windows.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using FactuurWizard.App.Services;
using Microsoft.Extensions.DependencyInjection;

namespace FactuurWizard.App.ViewModels;

public sealed record NavigatieItem(string Groep, string Titel, Type PaginaType)
{
    // Leesbare naam voor schermlezers (UI Automation gebruikt ToString).
    public override string ToString() => Titel;
}

public partial class ShellViewModel : ObservableObject
{
    private readonly IServiceProvider _services;
    private readonly DialoogService _dialogen;
    private bool _selectieOnderdrukken;

    public ShellViewModel(IServiceProvider services, DialoogService dialogen, Navigatie navigatie, Updater updater)
    {
        _services = services;
        _dialogen = dialogen;
        Updater = updater;
        navigatie.Shell = this;
    }

    public Updater Updater { get; }

    public string VersieTekst => $"Versie {Updater.HuidigeVersie}";

    [RelayCommand]
    private async Task BijwerkenAsync()
    {
        if (!MagPaginaVerlaten())
            return;
        // Pagina sluiten, zodat er geen databaseverbinding openstaat tijdens het bijwerken.
        HuidigePagina?.Dispose();
        try
        {
            await Updater.BijwerkenAsync();
        }
        catch (Exception ex)
        {
            Foutlog.Schrijf(ex);
            _dialogen.ToonFout($"Bijwerken is niet gelukt: {ex.Message}");
            if (GeselecteerdItem is { } item)
                await ToonAsync((PaginaViewModel)_services.GetRequiredService(item.PaginaType));
        }
    }

    public IReadOnlyList<NavigatieItem> Items { get; } =
    [
        new("Administratie", "Facturen", typeof(FacturenViewModel)),
        new("Administratie", "Klanten", typeof(KlantenViewModel)),
        new("Administratie", "Btw-overzicht", typeof(BtwOverzichtViewModel)),
        new("Inrichting", "Bedrijfsgegevens", typeof(BedrijfsgegevensViewModel)),
        new("Inrichting", "Btw-tarieven", typeof(BtwTarievenViewModel)),
        new("Inrichting", "Producten en diensten", typeof(ArtikelenViewModel)),
        new("Inrichting", "Briefpapier", typeof(BriefpapierViewModel)),
        new("Inrichting", "Vaste teksten", typeof(TekstenViewModel)),
        new("Inrichting", "Backup", typeof(BackupViewModel)),
    ];

    [ObservableProperty]
    public partial NavigatieItem? GeselecteerdItem { get; set; }

    [ObservableProperty]
    public partial PaginaViewModel? HuidigePagina { get; set; }

    /// <summary>Vraagt bevestiging als de huidige pagina niet-opgeslagen wijzigingen heeft.</summary>
    public bool MagPaginaVerlaten() =>
        HuidigePagina is not { HeeftWijzigingen: true } ||
        _dialogen.Bevestig("Er zijn niet-opgeslagen wijzigingen. Wilt u deze pagina verlaten zonder op te slaan?");

    partial void OnGeselecteerdItemChanged(NavigatieItem? oldValue, NavigatieItem? newValue)
    {
        if (_selectieOnderdrukken || newValue is null)
            return;

        if (!MagPaginaVerlaten())
        {
            // Selectie in de lijst terugzetten nadat de ListBox klaar is met de huidige wijziging.
            Dispatcher.CurrentDispatcher.BeginInvoke(() => ZetSelectie(oldValue));
            return;
        }

        _ = ToonAsync((PaginaViewModel)_services.GetRequiredService(newValue.PaginaType));
    }

    /// <summary>
    /// Opent een pagina vanuit code. Het menu-item van dat paginatype wordt geselecteerd, of geen
    /// als de pagina niet in het menu staat.
    /// </summary>
    public async Task<bool> OpenPaginaAsync(PaginaViewModel pagina)
    {
        if (!MagPaginaVerlaten())
        {
            pagina.Dispose();
            return false;
        }

        ZetSelectie(Items.FirstOrDefault(i => i.PaginaType == pagina.GetType()));
        await ToonAsync(pagina);
        return true;
    }

    private void ZetSelectie(NavigatieItem? item)
    {
        _selectieOnderdrukken = true;
        GeselecteerdItem = item;
        _selectieOnderdrukken = false;
    }

    private async Task ToonAsync(PaginaViewModel pagina)
    {
        HuidigePagina?.Dispose();
        HuidigePagina = pagina;
        try
        {
            await pagina.LaadAsync();
        }
        catch (Exception ex)
        {
            Foutlog.Schrijf(ex);
            _dialogen.ToonFout($"De pagina '{pagina.Titel}' kon niet worden geladen.\n\n{ex.Message}");
        }
    }
}
