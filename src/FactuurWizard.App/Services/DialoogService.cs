using System.Windows;
using FactuurWizard.App.Views;
using FactuurWizard.Core.Model;
using Microsoft.Win32;

namespace FactuurWizard.App.Services;

public class DialoogService
{
    public string? KiesBestand(string titel, string filter)
    {
        var dialoog = new OpenFileDialog { Title = titel, Filter = filter };
        return dialoog.ShowDialog(Application.Current.MainWindow) == true ? dialoog.FileName : null;
    }

    public string? KiesMap(string titel, string? startMap = null)
    {
        var dialoog = new OpenFolderDialog { Title = titel, InitialDirectory = startMap ?? "" };
        return dialoog.ShowDialog(Application.Current.MainWindow) == true ? dialoog.FolderName : null;
    }

    public bool Bevestig(string vraag, string titel = "FactuurWizard") =>
        MessageBox.Show(Application.Current.MainWindow, vraag, titel, MessageBoxButton.YesNo,
            MessageBoxImage.Question) == MessageBoxResult.Yes;

    public void Informeer(string tekst, string titel = "FactuurWizard") =>
        MessageBox.Show(Application.Current.MainWindow, tekst, titel, MessageBoxButton.OK,
            MessageBoxImage.Information);

    /// <summary>Toont het invoervenster voor een nieuwe klant; true als de gebruiker de klant toevoegt.</summary>
    public bool NieuweKlant(Klant klant) =>
        new NieuweKlantVenster(klant) { Owner = Application.Current.MainWindow }.ShowDialog() == true;

    public void ToonFout(string tekst, string titel = "FactuurWizard") =>
        MessageBox.Show(Application.Current.MainWindow, tekst, titel, MessageBoxButton.OK, MessageBoxImage.Error);
}
