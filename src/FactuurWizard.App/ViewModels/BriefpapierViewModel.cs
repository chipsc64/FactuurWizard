using System.IO;
using System.Windows.Media;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using FactuurWizard.App.Services;
using FactuurWizard.Core.Model;
using FactuurWizard.Core.Services;
using FactuurWizard.Data;
using Microsoft.EntityFrameworkCore;

namespace FactuurWizard.App.ViewModels;

public partial class BriefpapierViewModel(
    IDbContextFactory<FactuurWizardDbContext> dbFactory,
    DialoogService dialogen) : PaginaViewModel(dbFactory)
{
    private const int MaxBestandsgrootte = 10 * 1024 * 1024;
    private const string AfbeeldingFilter = "Afbeeldingen (*.png;*.jpg;*.jpeg)|*.png;*.jpg;*.jpeg";

    public override string Titel => "Briefpapier";

    [ObservableProperty]
    public partial Briefpapier? Sjabloon { get; set; }

    public IReadOnlyList<string> Lettertypen { get; } =
        Fonts.SystemFontFamilies.Select(f => f.Source).Order().ToList();

    public string AchtergrondOmschrijving => Sjabloon?.AchtergrondSoort switch
    {
        AchtergrondSoort.Afbeelding => $"Afbeelding ({Sjabloon.Achtergrond!.Length / 1024} kB)",
        AchtergrondSoort.Pdf => $"PDF-document ({Sjabloon.Achtergrond!.Length / 1024} kB) – eerste pagina wordt gebruikt",
        _ => "Geen achtergrond",
    };

    public override async Task LaadAsync()
    {
        Sjabloon = await Db.Briefpapieren.OrderByDescending(b => b.IsStandaard).FirstAsync();
    }

    private void Vernieuw()
    {
        OnPropertyChanged(nameof(Sjabloon));
        OnPropertyChanged(nameof(AchtergrondOmschrijving));
    }

    private byte[]? LeesBestand(string titel, string filter)
    {
        var pad = dialogen.KiesBestand(titel, filter);
        if (pad is null)
            return null;
        if (new FileInfo(pad).Length > MaxBestandsgrootte)
        {
            ToonFouten(["Het bestand is groter dan 10 MB. Kies een kleiner bestand."]);
            return null;
        }
        return File.ReadAllBytes(pad);
    }

    [RelayCommand]
    private void KiesLogo()
    {
        if (Sjabloon is null || LeesBestand("Kies een logo", AfbeeldingFilter) is not { } bytes)
            return;
        Sjabloon.Logo = bytes;
        Vernieuw();
    }

    [RelayCommand]
    private void VerwijderLogo()
    {
        if (Sjabloon is null)
            return;
        Sjabloon.Logo = null;
        Vernieuw();
    }

    [RelayCommand]
    private void KiesAchtergrond()
    {
        if (Sjabloon is null)
            return;
        var bytes = LeesBestand("Kies uw briefpapier",
            "Briefpapier (*.pdf;*.png;*.jpg;*.jpeg)|*.pdf;*.png;*.jpg;*.jpeg");
        if (bytes is null)
            return;

        Sjabloon.Achtergrond = bytes;
        Sjabloon.AchtergrondSoort = IsPdf(bytes) ? AchtergrondSoort.Pdf : AchtergrondSoort.Afbeelding;
        if (Sjabloon.ToonBedrijfsgegevens &&
            dialogen.Bevestig("Staan uw bedrijfsgegevens en logo al op dit briefpapier? Dan worden ze niet " +
                              "nogmaals op de factuur gezet."))
            Sjabloon.ToonBedrijfsgegevens = false;
        Vernieuw();
    }

    private static bool IsPdf(byte[] bytes) => bytes is [(byte)'%', (byte)'P', (byte)'D', (byte)'F', ..];

    [RelayCommand]
    private void VerwijderAchtergrond()
    {
        if (Sjabloon is null)
            return;
        Sjabloon.Achtergrond = null;
        Sjabloon.AchtergrondSoort = AchtergrondSoort.Geen;
        Vernieuw();
    }

    [RelayCommand]
    private async Task OpslaanAsync()
    {
        if (Sjabloon is not { } s)
            return;

        var fouten = new List<string>();
        if (!Validatie.IsGeldigeKleur(s.Accentkleur))
            fouten.Add("De accentkleur moet het formaat #RRGGBB hebben, bijv. #1F4E79.");
        if (!Validatie.IsGeldigeKleur(s.Tekstkleur))
            fouten.Add("De tekstkleur moet het formaat #RRGGBB hebben, bijv. #000000.");
        if (new[] { s.MargeBovenMm, s.MargeOnderMm, s.MargeLinksMm, s.MargeRechtsMm }.Any(m => m is < 5 or > 100))
            fouten.Add("Marges moeten tussen 5 en 100 mm liggen.");
        if (s.LogoBreedteMm is < 10 or > 150)
            fouten.Add("De logobreedte moet tussen 10 en 150 mm liggen.");

        if (fouten.Count > 0)
        {
            ToonFouten(fouten);
            return;
        }

        await Db.SaveChangesAsync();
        Vernieuw();
        ToonSucces("Briefpapier opgeslagen.");
    }
}
