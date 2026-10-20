using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace FactuurWizard.Data;

/// <summary>
/// Alleen gebruikt door 'dotnet ef' bij het aanmaken van migraties.
/// </summary>
public class DesignTimeDbContextFactory : IDesignTimeDbContextFactory<FactuurWizardDbContext>
{
    public FactuurWizardDbContext CreateDbContext(string[] args)
    {
        var options = new DbContextOptionsBuilder<FactuurWizardDbContext>()
            .UseSqlite("Data Source=design.db")
            .Options;
        return new FactuurWizardDbContext(options);
    }
}
