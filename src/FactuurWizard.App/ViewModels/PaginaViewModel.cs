using CommunityToolkit.Mvvm.ComponentModel;
using FactuurWizard.Data;
using Microsoft.EntityFrameworkCore;

namespace FactuurWizard.App.ViewModels;

/// <summary>
/// Basis voor een pagina. Elke pagina heeft een eigen DbContext die leeft zolang de pagina open is,
/// zodat niet-opgeslagen wijzigingen herkend kunnen worden.
/// </summary>
public abstract partial class PaginaViewModel : ObservableObject, IDisposable
{
    protected PaginaViewModel(IDbContextFactory<FactuurWizardDbContext> dbFactory)
    {
        Db = dbFactory.CreateDbContext();
    }

    protected FactuurWizardDbContext Db { get; }

    public abstract string Titel { get; }

    [ObservableProperty]
    public partial string? Melding { get; set; }

    [ObservableProperty]
    public partial bool MeldingIsFout { get; set; }

    private bool _opgeruimd;

    public virtual bool HeeftWijzigingen => !_opgeruimd && Db.ChangeTracker.HasChanges();

    public abstract Task LaadAsync();

    protected void ToonSucces(string tekst)
    {
        MeldingIsFout = false;
        Melding = tekst;
    }

    protected void ToonFouten(IReadOnlyCollection<string> fouten)
    {
        MeldingIsFout = true;
        Melding = string.Join(Environment.NewLine, fouten);
    }

    public void Dispose()
    {
        _opgeruimd = true;
        Db.Dispose();
        GC.SuppressFinalize(this);
    }
}
