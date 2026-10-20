using System.Diagnostics;
using System.Windows;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using FactuurWizard.App.Services;
using FactuurWizard.Core.Model;
using FactuurWizard.Core.Services;
using FactuurWizard.Data;
using Microsoft.EntityFrameworkCore;

namespace FactuurWizard.App.ViewModels;

public partial class BedrijfsgegevensViewModel(
    IDbContextFactory<FactuurWizardDbContext> dbFactory,
    Opslaglocatie opslag,
    DialoogService dialogen) : PaginaViewModel(dbFactory)
{
    public override string Titel => "Bedrijfsgegevens";

    [ObservableProperty]
    public partial Bedrijfsprofiel? Profiel { get; set; }

    [ObservableProperty]
    public partial bool KorActief { get; set; }

    /// <summary>Zodra er facturen genummerd zijn, kan het startnummer niet meer gewijzigd worden.</summary>
    [ObservableProperty]
    public partial bool NummeringVergrendeld { get; set; }

    public string DataMap => opslag.DataMap;

    public override async Task LaadAsync()
    {
        Profiel = await Db.Bedrijfsprofielen.SingleAsync();
        KorActief = Profiel.KorIngangsdatum is not null;
        NummeringVergrendeld = await Db.Nummerreeksen.AnyAsync();
    }

    partial void OnKorActiefChanged(bool value)
    {
        if (Profiel is null)
            return;
        if (value)
            Profiel.KorIngangsdatum ??= DateOnly.FromDateTime(DateTime.Today);
        else
            Profiel.KorIngangsdatum = Profiel.KorEinddatum = null;
        OnPropertyChanged(nameof(Profiel));
    }

    [RelayCommand]
    private async Task OpslaanAsync()
    {
        if (Profiel is not { } p)
            return;

        Normaliseer(p);
        var fouten = Valideer(p);
        if (fouten.Count > 0)
        {
            ToonFouten(fouten);
            return;
        }

        await Db.SaveChangesAsync();
        OnPropertyChanged(nameof(Profiel));
        ToonSucces("Bedrijfsgegevens opgeslagen.");
    }

    private static void Normaliseer(Bedrijfsprofiel p)
    {
        p.Iban = string.IsNullOrWhiteSpace(p.Iban) ? "" : Validatie.FormatteerIban(p.Iban);
        p.Bic = p.Bic.Trim().ToUpperInvariant();
        p.BtwNummer = Validatie.Compact(p.BtwNummer).Replace(".", "");
        p.KvkNummer = Validatie.Compact(p.KvkNummer);
        p.Adres.Postcode = Validatie.FormatteerPostcode(p.Adres.Postcode);
    }

    private List<string> Valideer(Bedrijfsprofiel p)
    {
        var fouten = new List<string>();
        if (string.IsNullOrWhiteSpace(p.Bedrijfsnaam))
            fouten.Add("Vul de bedrijfsnaam in.");
        if (p.Iban.Length > 0 && !Validatie.IsGeldigeIban(p.Iban))
            fouten.Add("Het IBAN is niet geldig.");
        if (p.BtwNummer.Length > 0 && !Validatie.IsGeldigBtwNummer(p.BtwNummer))
            fouten.Add("Het btw-nummer is niet geldig (formaat NL123456789B01).");
        if (p.KvkNummer.Length > 0 && !Validatie.IsGeldigKvkNummer(p.KvkNummer))
            fouten.Add("Het KvK-nummer moet uit 8 cijfers bestaan.");
        if (p.Adres.Postcode.Length > 0 && p.Adres.Land.Trim() == "Nederland" && !Validatie.IsGeldigePostcode(p.Adres.Postcode))
            fouten.Add("De postcode is niet geldig (formaat 1234 AB).");
        if (p.Email.Length > 0 && !Validatie.IsGeldigEmailadres(p.Email))
            fouten.Add("Het e-mailadres is niet geldig.");
        if (p.StandaardBetalingstermijnDagen is < 0 or > 365)
            fouten.Add("De betalingstermijn moet tussen 0 en 365 dagen liggen.");
        if (p.NummeringStartnummer is < 1 or > 9999)
            fouten.Add("Het startnummer moet tussen 1 en 9999 liggen.");
        if (p.NummeringEersteJaar is < 2000 or > 2100)
            fouten.Add("Het jaar voor het startnummer is niet geldig.");
        if (KorActief && p.KorIngangsdatum is null)
            fouten.Add("Vul de ingangsdatum van de KOR in.");
        if (p.KorIngangsdatum is { } start && p.KorEinddatum is { } eind && eind < start)
            fouten.Add("De einddatum van de KOR ligt vóór de ingangsdatum.");
        return fouten;
    }

    [RelayCommand]
    private void WijzigOpslaglocatie()
    {
        if (HeeftWijzigingen)
        {
            dialogen.Informeer("Sla eerst uw wijzigingen op.");
            return;
        }

        var nieuweMap = dialogen.KiesMap("Kies de map voor uw facturatiegegevens", opslag.DataMap);
        if (nieuweMap is null)
            return;

        try
        {
            if (Opslaglocatie.BevatDatabase(nieuweMap))
            {
                if (!dialogen.Bevestig("In deze map staan al FactuurWizard-gegevens. Wilt u die gegevens " +
                                       "gebruiken? Uw huidige gegevens worden dan niet gekopieerd."))
                    return;
            }
            else
            {
                if (!dialogen.Bevestig($"Uw gegevens worden gekopieerd naar:\n{nieuweMap}\n\n" +
                                       "De huidige map blijft als reservekopie bestaan. Doorgaan?"))
                    return;
                Dispose();
                opslag.KopieerNaar(nieuweMap);
            }

            Opslaglocatie.Bewaar(nieuweMap);
        }
        catch (Exception ex)
        {
            dialogen.ToonFout($"De opslaglocatie kon niet worden gewijzigd.\n\n{ex.Message}");
            return;
        }

        dialogen.Informeer("De opslaglocatie is gewijzigd. FactuurWizard wordt opnieuw gestart.");
        Process.Start(Environment.ProcessPath!);
        Application.Current.Shutdown();
    }
}
