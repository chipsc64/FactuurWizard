using System.Collections.ObjectModel;
using System.Windows.Data;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using FactuurWizard.Core.Model;
using FactuurWizard.Core.Services;
using FactuurWizard.Data;
using Microsoft.EntityFrameworkCore;

namespace FactuurWizard.App.ViewModels;

public sealed class BtwGroepItem(BtwGroep groep)
{
    public BtwGroep Groep { get; } = groep;
    public ObservableCollection<BtwTarief> Tarieven { get; } = new(groep.Tarieven.OrderBy(t => t.Ingangsdatum));

    public string Omschrijving
    {
        get
        {
            var vandaag = Groep.Tarieven.FirstOrDefault(t => t.GeldigOp(DateOnly.FromDateTime(DateTime.Today)));
            return vandaag is null ? $"{Groep.Naam} (geen geldig tarief)" : $"{Groep.Naam} ({vandaag.Percentage:0.##}%)";
        }
    }

    public void Ververs() => CollectionViewSource.GetDefaultView(Tarieven).Refresh();
}

public partial class BtwTarievenViewModel(IDbContextFactory<FactuurWizardDbContext> dbFactory)
    : PaginaViewModel(dbFactory)
{
    public override string Titel => "Btw-tarieven";

    public ObservableCollection<BtwGroepItem> Groepen { get; } = [];

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(VoegTariefToeCommand))]
    public partial BtwGroepItem? GeselecteerdeGroep { get; set; }

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(VerwijderTariefCommand))]
    public partial BtwTarief? GeselecteerdTarief { get; set; }

    [ObservableProperty]
    public partial decimal? NieuwPercentage { get; set; }

    [ObservableProperty]
    public partial DateTime? NieuweIngangsdatum { get; set; }

    public override async Task LaadAsync()
    {
        var groepen = await Db.BtwGroepen.Include(g => g.Tarieven).OrderBy(g => g.Soort).ToListAsync();
        Groepen.Clear();
        foreach (var groep in groepen)
            Groepen.Add(new BtwGroepItem(groep));
        GeselecteerdeGroep = Groepen.FirstOrDefault();
    }

    [RelayCommand(CanExecute = nameof(IsGroepGeselecteerd))]
    private void VoegTariefToe()
    {
        if (GeselecteerdeGroep is not { } item)
            return;

        if (NieuwPercentage is not { } percentage || percentage is < 0 or > 100)
        {
            ToonFouten(["Vul een percentage tussen 0 en 100 in."]);
            return;
        }
        if (NieuweIngangsdatum is not { } datum)
        {
            ToonFouten(["Vul de ingangsdatum in."]);
            return;
        }

        var ingang = DateOnly.FromDateTime(datum);
        var laatste = item.Tarieven.LastOrDefault();
        if (laatste is not null && ingang <= laatste.Ingangsdatum)
        {
            ToonFouten([$"De ingangsdatum moet na {laatste.Ingangsdatum:dd-MM-yyyy} liggen. " +
                        "Pas oudere tarieven zo nodig direct in de tabel aan."]);
            return;
        }

        // Het lopende tarief eindigt op de dag vóór het nieuwe tarief.
        if (laatste is not null && (laatste.Einddatum is null || laatste.Einddatum >= ingang))
            laatste.Einddatum = ingang.AddDays(-1);

        var tarief = new BtwTarief { Percentage = percentage, Ingangsdatum = ingang };
        item.Groep.Tarieven.Add(tarief);
        item.Tarieven.Add(tarief);
        item.Ververs();

        NieuwPercentage = null;
        NieuweIngangsdatum = null;
        ToonSucces("Tarief toegevoegd. Klik op Opslaan om de wijziging te bewaren.");
    }

    private bool IsGroepGeselecteerd() => GeselecteerdeGroep is not null;

    [RelayCommand(CanExecute = nameof(IsTariefGeselecteerd))]
    private void VerwijderTarief()
    {
        if (GeselecteerdeGroep is not { } item || GeselecteerdTarief is not { } tarief)
            return;

        if (item.Tarieven.Count == 1)
        {
            ToonFouten(["Een btw-groep moet minimaal één tarief hebben."]);
            return;
        }

        var index = item.Tarieven.IndexOf(tarief);
        item.Groep.Tarieven.Remove(tarief);
        item.Tarieven.Remove(tarief);

        // Was dit het lopende tarief, dan loopt het vorige tarief weer door.
        if (tarief.Einddatum is null && index > 0)
            item.Tarieven[index - 1].Einddatum = null;
        item.Ververs();
        ToonSucces("Tarief verwijderd. Klik op Opslaan om de wijziging te bewaren.");
    }

    private bool IsTariefGeselecteerd() => GeselecteerdTarief is not null;

    [RelayCommand]
    private async Task OpslaanAsync()
    {
        var fouten = new List<string>();
        foreach (var item in Groepen)
        {
            if (item.Tarieven.Any(t => t.Percentage is < 0 or > 100))
                fouten.Add($"{item.Groep.Naam}: percentages moeten tussen 0 en 100 liggen.");
            fouten.AddRange(BtwTariefBepaling.Valideer(item.Tarieven).Select(f => $"{item.Groep.Naam}: {f}"));
        }

        if (fouten.Count > 0)
        {
            ToonFouten(fouten);
            return;
        }

        await Db.SaveChangesAsync();
        foreach (var item in Groepen)
            item.Ververs();
        OnPropertyChanged(nameof(Groepen));
        ToonSucces("Btw-tarieven opgeslagen. Nieuwe tarieven gelden voor facturen die nog definitief gemaakt worden.");
    }
}
