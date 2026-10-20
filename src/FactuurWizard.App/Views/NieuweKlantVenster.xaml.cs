using System.Windows;
using FactuurWizard.Core.Model;
using FactuurWizard.Core.Services;

namespace FactuurWizard.App.Views;

public partial class NieuweKlantVenster : Window
{
    private readonly Klant _klant;

    public NieuweKlantVenster(Klant klant)
    {
        InitializeComponent();
        DataContext = _klant = klant;
        Loaded += (_, _) => NaamVeld.Focus();
    }

    private void Soort_Gewijzigd(object sender, RoutedEventArgs e)
    {
        // Wordt al tijdens InitializeComponent aangeroepen, voordat alle elementen bestaan.
        if (BedrijfVelden is null || NaamLabel is null)
            return;
        var bedrijf = Bedrijf.IsChecked == true;
        BedrijfVelden.Visibility = bedrijf ? Visibility.Visible : Visibility.Collapsed;
        NaamLabel.Text = bedrijf ? "Bedrijfsnaam *" : "Naam *";
    }

    private void Toevoegen_Click(object sender, RoutedEventArgs e)
    {
        // Focus verplaatsen zodat het laatst bewerkte veld zijn waarde doorgeeft.
        ((UIElement)sender).Focus();

        _klant.Soort = Bedrijf.IsChecked == true ? KlantSoort.Bedrijf : KlantSoort.Particulier;
        if (_klant.Soort == KlantSoort.Particulier)
            _klant.Contactpersoon = "";

        KlantValidatie.Normaliseer(_klant);
        var fouten = KlantValidatie.Valideer(_klant).ToList();
        if (fouten.Count > 0)
        {
            Fouttekst.Text = string.Join(Environment.NewLine, fouten.Select(f => char.ToUpper(f[0]) + f[1..]));
            return;
        }
        DialogResult = true;
    }
}
