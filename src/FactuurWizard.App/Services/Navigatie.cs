using FactuurWizard.App.ViewModels;
using Microsoft.Extensions.DependencyInjection;

namespace FactuurWizard.App.Services;

/// <summary>
/// Laat pagina's naar een andere pagina navigeren, ook naar pagina's die niet in het menu staan.
/// </summary>
public class Navigatie(IServiceProvider services)
{
    public ShellViewModel? Shell { get; set; }

    public async Task<bool> OpenAsync<T>(Action<T>? voorbereiden = null) where T : PaginaViewModel
    {
        var pagina = services.GetRequiredService<T>();
        voorbereiden?.Invoke(pagina);
        return await Shell!.OpenPaginaAsync(pagina);
    }
}
