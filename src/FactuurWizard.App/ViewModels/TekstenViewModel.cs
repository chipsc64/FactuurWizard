using CommunityToolkit.Mvvm.Input;
using FactuurWizard.Core.Model;
using FactuurWizard.Core.Services;
using FactuurWizard.Data;
using Microsoft.EntityFrameworkCore;

namespace FactuurWizard.App.ViewModels;

public sealed record TekstItem(string Groep, string Titel, bool Meerregelig, Tekstsjabloon Sjabloon);

public partial class TekstenViewModel(IDbContextFactory<FactuurWizardDbContext> dbFactory) : PaginaViewModel(dbFactory)
{
    private static readonly Dictionary<TekstSoort, (string Groep, string Titel, bool Meerregelig)> Omschrijvingen = new()
    {
        [TekstSoort.Betalingsvoorwaarden] = ("Factuur", "Betalingsvoorwaarden", true),
        [TekstSoort.Afsluittekst] = ("Factuur", "Afsluittekst", true),
        [TekstSoort.AlgemeneVoorwaarden] = ("Factuur", "Algemene voorwaarden (kleine tekst onderaan)", true),
        [TekstSoort.EmailOnderwerp] = ("E-mail bij factuur", "Onderwerp", false),
        [TekstSoort.EmailTekst] = ("E-mail bij factuur", "Tekst", true),
        [TekstSoort.Herinnering1Tekst] = ("Herinneringen", "1e herinnering", true),
        [TekstSoort.Herinnering2Tekst] = ("Herinneringen", "2e herinnering / aanmaning", true),
        [TekstSoort.HerinneringEmailOnderwerp] = ("Herinneringen", "Onderwerp e-mail", false),
        [TekstSoort.HerinneringEmailTekst] = ("Herinneringen", "Tekst e-mail", true),
    };

    public override string Titel => "Vaste teksten";

    public IReadOnlyList<TekstItem> Teksten { get; private set; } = [];

    public string PlaatshouderUitleg { get; } =
        string.Join(Environment.NewLine, Plaatshouders.Beschikbaar.Select(p => $"{{{p.Naam}}}  –  {p.Uitleg}"));

    public override async Task LaadAsync()
    {
        var sjablonen = await Db.Tekstsjablonen.Where(t => t.Taal == "nl-NL").ToListAsync();

        // Ontbrekende soorten (bijv. na een update met nieuwe teksten) leeg aanvullen.
        foreach (var soort in Enum.GetValues<TekstSoort>().Where(s => sjablonen.All(t => t.Soort != s)))
        {
            var nieuw = new Tekstsjabloon { Soort = soort };
            Db.Tekstsjablonen.Add(nieuw);
            sjablonen.Add(nieuw);
        }
        if (Db.ChangeTracker.HasChanges())
            await Db.SaveChangesAsync();

        Teksten = Omschrijvingen
            .Select(o => new TekstItem(o.Value.Groep, o.Value.Titel, o.Value.Meerregelig,
                sjablonen.Single(t => t.Soort == o.Key)))
            .ToList();
        OnPropertyChanged(nameof(Teksten));
    }

    [RelayCommand]
    private async Task OpslaanAsync()
    {
        await Db.SaveChangesAsync();
        ToonSucces("Vaste teksten opgeslagen.");
    }
}
