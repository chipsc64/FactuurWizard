using System.Windows.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using FactuurWizard.App.Services;
using Microsoft.Extensions.DependencyInjection;

namespace FactuurWizard.App.ViewModels;

public sealed record NavigatieItem(string Groep, string Titel, Type PaginaType);

public partial class ShellViewModel(IServiceProvider services, DialoogService dialogen) : ObservableObject
{
    private bool _terugzetten;

    public IReadOnlyList<NavigatieItem> Items { get; } =
    [
        new("Administratie", "Klanten", typeof(KlantenViewModel)),
        new("Inrichting", "Bedrijfsgegevens", typeof(BedrijfsgegevensViewModel)),
        new("Inrichting", "Btw-tarieven", typeof(BtwTarievenViewModel)),
        new("Inrichting", "Producten en diensten", typeof(ArtikelenViewModel)),
        new("Inrichting", "Briefpapier", typeof(BriefpapierViewModel)),
        new("Inrichting", "Vaste teksten", typeof(TekstenViewModel)),
    ];

    [ObservableProperty]
    public partial NavigatieItem? GeselecteerdItem { get; set; }

    [ObservableProperty]
    public partial PaginaViewModel? HuidigePagina { get; set; }

    /// <summary>Vraagt bevestiging als de huidige pagina niet-opgeslagen wijzigingen heeft.</summary>
    public bool MagPaginaVerlaten() =>
        HuidigePagina is not { HeeftWijzigingen: true } ||
        dialogen.Bevestig("Er zijn niet-opgeslagen wijzigingen. Wilt u deze pagina verlaten zonder op te slaan?");

    partial void OnGeselecteerdItemChanged(NavigatieItem? oldValue, NavigatieItem? newValue)
    {
        if (_terugzetten || newValue is null)
            return;

        if (!MagPaginaVerlaten())
        {
            // Selectie in de lijst terugzetten nadat de ListBox klaar is met de huidige wijziging.
            Dispatcher.CurrentDispatcher.BeginInvoke(() =>
            {
                _terugzetten = true;
                GeselecteerdItem = oldValue;
                _terugzetten = false;
            });
            return;
        }

        _ = OpenAsync(newValue);
    }

    private async Task OpenAsync(NavigatieItem item)
    {
        HuidigePagina?.Dispose();
        var pagina = (PaginaViewModel)services.GetRequiredService(item.PaginaType);
        HuidigePagina = pagina;
        try
        {
            await pagina.LaadAsync();
        }
        catch (Exception ex)
        {
            Foutlog.Schrijf(ex);
            dialogen.ToonFout($"De pagina '{item.Titel}' kon niet worden geladen.\n\n{ex.Message}");
        }
    }
}
