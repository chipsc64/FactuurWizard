using Velopack;

namespace FactuurWizard.App;

public static class Program
{
    [STAThread]
    public static void Main(string[] args)
    {
        // Moet als eerste: handelt installeren, bijwerken en verwijderen af (en stopt dan zelf het proces).
        VelopackApp.Build().Run();

        var app = new App();
        app.InitializeComponent();
        app.Run();
    }
}
