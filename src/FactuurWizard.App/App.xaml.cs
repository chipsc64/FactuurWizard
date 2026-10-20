using System.IO;
using System.Windows;
using FactuurWizard.Data;
using FactuurWizard.Data.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace FactuurWizard.App;

public partial class App : Application
{
    private IHost? _host;

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        var opslag = Opslaglocatie.Laad();
        Directory.CreateDirectory(opslag.DataMap);

        _host = Host.CreateDefaultBuilder()
            .ConfigureServices(services =>
            {
                services.AddSingleton(opslag);
                services.AddDbContext<FactuurWizardDbContext>(
                    o => o.UseSqlite(opslag.ConnectionString), ServiceLifetime.Transient);
                services.AddTransient<FactuurService>();
                services.AddSingleton<MainWindow>();
            })
            .Build();

        try
        {
            using var db = _host.Services.GetRequiredService<FactuurWizardDbContext>();
            DatabaseInitialisatie.Initialiseer(db);
        }
        catch (Exception ex)
        {
            MessageBox.Show($"De database kon niet worden geopend:\n{opslag.DatabasePad}\n\n{ex.Message}",
                "FactuurWizard", MessageBoxButton.OK, MessageBoxImage.Error);
            Shutdown(1);
            return;
        }

        _host.Services.GetRequiredService<MainWindow>().Show();
    }

    protected override void OnExit(ExitEventArgs e)
    {
        _host?.Dispose();
        base.OnExit(e);
    }
}
