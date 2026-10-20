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

    private void Window_Closing(object? sender, CancelEventArgs e)
    {
        if (!_shell.MagPaginaVerlaten())
            e.Cancel = true;
    }
}
