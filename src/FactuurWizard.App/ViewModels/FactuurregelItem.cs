using CommunityToolkit.Mvvm.ComponentModel;
using FactuurWizard.Core.Model;
using FactuurWizard.Core.Services;

namespace FactuurWizard.App.ViewModels;

/// <summary>
/// Bewerkbare factuurregel; meldt elke wijziging die het bedrag of de btw raakt aan de factuur.
/// </summary>
public sealed class FactuurregelItem(Factuurregel regel, Action gewijzigd) : ObservableObject
{
    public Factuurregel Regel { get; } = regel;

    public string SoortOmschrijving => Regel.Soort switch
    {
        RegelSoort.Product => "Product",
        RegelSoort.Dienst => "Dienst",
        _ => "Vrij",
    };

    public string Artikelnummer
    {
        get => Regel.Artikelnummer;
        set => Zet(Regel.Artikelnummer, value, v => Regel.Artikelnummer = v, false);
    }

    public string Omschrijving
    {
        get => Regel.Omschrijving;
        set => Zet(Regel.Omschrijving, value, v => Regel.Omschrijving = v, false);
    }

    public decimal Aantal
    {
        get => Regel.Aantal;
        set => Zet(Regel.Aantal, value, v => Regel.Aantal = v, true);
    }

    public string Eenheid
    {
        get => Regel.Eenheid;
        set => Zet(Regel.Eenheid, value, v => Regel.Eenheid = v, false);
    }

    public decimal PrijsExclBtw
    {
        get => Regel.PrijsExclBtw;
        set => Zet(Regel.PrijsExclBtw, value, v => Regel.PrijsExclBtw = v, true);
    }

    public int BtwGroepId
    {
        get => Regel.BtwGroepId;
        set => Zet(Regel.BtwGroepId, value, v => Regel.BtwGroepId = v, true);
    }

    public decimal BtwPercentage => Regel.BtwPercentage;
    public decimal Bedrag => FactuurBerekening.Regelbedrag(Regel);

    public void VerversBtw() => OnPropertyChanged(nameof(BtwPercentage));

    private void Zet<T>(T huidig, T nieuw, Action<T> zetter, bool raaktBedrag, [System.Runtime.CompilerServices.CallerMemberName] string? naam = null)
    {
        if (EqualityComparer<T>.Default.Equals(huidig, nieuw))
            return;
        zetter(nieuw);
        OnPropertyChanged(naam);
        if (!raaktBedrag)
            return;
        OnPropertyChanged(nameof(Bedrag));
        gewijzigd();
    }
}
