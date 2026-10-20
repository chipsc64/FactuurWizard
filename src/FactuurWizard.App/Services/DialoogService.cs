using System.Windows;
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

    public void ToonFout(string tekst, string titel = "FactuurWizard") =>
        MessageBox.Show(Application.Current.MainWindow, tekst, titel, MessageBoxButton.OK, MessageBoxImage.Error);
}
