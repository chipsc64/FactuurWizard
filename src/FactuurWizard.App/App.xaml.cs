using System.Globalization;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Markup;
using FactuurWizard.App.Services;
using FactuurWizard.App.ViewModels;
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
        StelCultuurIn();
        DispatcherUnhandledException += (_, args) =>
        {
            Foutlog.Schrijf(args.Exception);
            MessageBox.Show($"Er is een onverwachte fout opgetreden:\n\n{args.Exception.Message}\n\n" +
                            $"Details staan in {Foutlog.Pad}", "FactuurWizard", MessageBoxButton.OK, MessageBoxImage.Error);
            args.Handled = true;
        };

        var opslag = Opslaglocatie.Laad();

        _host = Host.CreateDefaultBuilder()
            .ConfigureServices(services =>
            {
                services.AddSingleton(opslag);
                services.AddDbContextFactory<FactuurWizardDbContext>(o => o.UseSqlite(opslag.ConnectionString));
                services.AddSingleton<DialoogService>();
                services.AddSingleton<Navigatie>();

                services.AddSingleton<ShellViewModel>();
                services.AddTransient<FacturenViewModel>();
                services.AddTransient<FactuurBewerkenViewModel>();
                services.AddTransient<KlantenViewModel>();
                services.AddTransient<BedrijfsgegevensViewModel>();
                services.AddTransient<BtwTarievenViewModel>();
                services.AddTransient<ArtikelenViewModel>();
                services.AddTransient<BriefpapierViewModel>();
                services.AddTransient<TekstenViewModel>();

                services.AddSingleton<MainWindow>();
            })
            .Build();

        try
        {
            Directory.CreateDirectory(opslag.DataMap);
            using var db = _host.Services.GetRequiredService<IDbContextFactory<FactuurWizardDbContext>>().CreateDbContext();
            DatabaseInitialisatie.Initialiseer(db);
        }
        catch (Exception ex)
        {
            MessageBox.Show($"De database kon niet worden geopend:\n{opslag.DatabasePad}\n\n{ex.Message}",
                "FactuurWizard", MessageBoxButton.OK, MessageBoxImage.Error);
            Shutdown(1);
            return;
        }

        var venster = _host.Services.GetRequiredService<MainWindow>();
        MainWindow = venster;
        venster.Show();
    }

    /// <summary>Nederlandse notatie voor bedragen en datums, ook in bindings (komma als decimaalteken).</summary>
    private static void StelCultuurIn()
    {
        var nl = new CultureInfo("nl-NL");
        CultureInfo.DefaultThreadCurrentCulture = nl;
        CultureInfo.DefaultThreadCurrentUICulture = nl;
        Thread.CurrentThread.CurrentCulture = nl;
        Thread.CurrentThread.CurrentUICulture = nl;
        var taal = XmlLanguage.GetLanguage(nl.IetfLanguageTag);
        FrameworkElement.LanguageProperty.OverrideMetadata(typeof(FrameworkElement), new FrameworkPropertyMetadata(taal));
        // Ook voor tekstonderdelen als <Run>, anders gebruiken die de Engelse notatie ($1.00).
        FrameworkContentElement.LanguageProperty.OverrideMetadata(typeof(System.Windows.Documents.TextElement),
            new FrameworkPropertyMetadata(taal));

        // Datumvelden zonder de Engelse watermerktekst "Select a date".
        EventManager.RegisterClassHandler(typeof(DatePicker), FrameworkElement.LoadedEvent, new RoutedEventHandler((s, _) =>
        {
            if (s is DatePicker dp &&
                dp.Template.FindName("PART_TextBox", dp) is DatePickerTextBox tekstvak &&
                tekstvak.Template.FindName("PART_Watermark", tekstvak) is ContentControl watermerk)
                watermerk.Content = "";
        }));
    }

    protected override void OnExit(ExitEventArgs e)
    {
        _host?.Dispose();
        base.OnExit(e);
    }
}
