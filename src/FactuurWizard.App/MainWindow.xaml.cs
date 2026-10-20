using System.Windows;
using FactuurWizard.Data;

namespace FactuurWizard.App;

public partial class MainWindow : Window
{
    public MainWindow(Opslaglocatie opslag)
    {
        InitializeComponent();
        DataMapTekst.Text = $"Gegevens: {opslag.DataMap}";
    }
}
