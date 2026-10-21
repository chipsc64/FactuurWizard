using System.ComponentModel;
using System.Windows;
using FactuurWizard.App.ViewModels;

namespace FactuurWizard.App;

public partial class MainWindow : Window
{
    private readonly ShellViewModel _shell;

    public MainWindow(ShellViewModel shell)
    {
        InitializeComponent();
        DataContext = _shell = shell;
        shell.GeselecteerdItem = shell.Items[0];
    }

    private void Link_RequestNavigate(object sender, System.Windows.Navigation.RequestNavigateEventArgs e)
    {
        System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(e.Uri.AbsoluteUri) { UseShellExecute = true });
        e.Handled = true;
    }

    private void Window_Closing(object? sender, CancelEventArgs e)
    {
        if (!_shell.MagPaginaVerlaten())
            e.Cancel = true;
    }
}
