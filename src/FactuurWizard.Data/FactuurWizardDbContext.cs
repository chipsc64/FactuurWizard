using FactuurWizard.Core.Model;
using Microsoft.EntityFrameworkCore;

namespace FactuurWizard.Data;

public class FactuurWizardDbContext(DbContextOptions<FactuurWizardDbContext> options) : DbContext(options)
{
    public DbSet<Bedrijfsprofiel> Bedrijfsprofielen => Set<Bedrijfsprofiel>();
    public DbSet<Klant> Klanten => Set<Klant>();
    public DbSet<BtwGroep> BtwGroepen => Set<BtwGroep>();
    public DbSet<BtwTarief> BtwTarieven => Set<BtwTarief>();
    public DbSet<Artikel> Artikelen => Set<Artikel>();
    public DbSet<Factuur> Facturen => Set<Factuur>();
    public DbSet<Factuurregel> Factuurregels => Set<Factuurregel>();
    public DbSet<Herinnering> Herinneringen => Set<Herinnering>();
    public DbSet<Nummerreeks> Nummerreeksen => Set<Nummerreeks>();
    public DbSet<Briefpapier> Briefpapieren => Set<Briefpapier>();
    public DbSet<Tekstsjabloon> Tekstsjablonen => Set<Tekstsjabloon>();

    protected override void OnModelCreating(ModelBuilder model)
    {
        model.Entity<Bedrijfsprofiel>(e =>
        {
            e.ToTable("Bedrijfsprofiel");
            e.OwnsOne(p => p.Adres);
        });

        model.Entity<Klant>(e =>
        {
            e.ToTable("Klant");
            e.HasIndex(k => k.Klantnummer).IsUnique();
            e.HasIndex(k => k.Naam);
            e.OwnsOne(k => k.Adres);
            e.OwnsOne(k => k.Factuuradres);
        });

        model.Entity<BtwGroep>(e =>
        {
            e.ToTable("BtwGroep");
            e.HasMany(g => g.Tarieven).WithOne().HasForeignKey(t => t.BtwGroepId).OnDelete(DeleteBehavior.Cascade);
        });
        model.Entity<BtwTarief>().ToTable("BtwTarief");

        model.Entity<Artikel>(e =>
        {
            e.ToTable("Artikel");
            e.HasIndex(a => a.Artikelnummer);
            e.HasOne(a => a.BtwGroep).WithMany().HasForeignKey(a => a.BtwGroepId).OnDelete(DeleteBehavior.Restrict);
        });

        model.Entity<Factuur>(e =>
        {
            e.ToTable("Factuur");
            e.HasIndex(f => f.Factuurnummer).IsUnique();
            e.HasIndex(f => f.Factuurdatum);
            e.HasOne(f => f.Klant).WithMany().HasForeignKey(f => f.KlantId).OnDelete(DeleteBehavior.Restrict);
            e.HasOne(f => f.OrigineleFactuur).WithMany().HasForeignKey(f => f.OrigineleFactuurId)
                .OnDelete(DeleteBehavior.Restrict);
            e.HasOne<Briefpapier>().WithMany().HasForeignKey(f => f.BriefpapierId).OnDelete(DeleteBehavior.SetNull);
            e.OwnsOne(f => f.KlantGegevens, o => o.OwnsOne(p => p.Adres));
            e.OwnsOne(f => f.LeverancierGegevens, o => o.OwnsOne(p => p.Adres));
            e.HasMany(f => f.Regels).WithOne().HasForeignKey(r => r.FactuurId).OnDelete(DeleteBehavior.Cascade);
            e.HasMany(f => f.Herinneringen).WithOne().HasForeignKey(h => h.FactuurId).OnDelete(DeleteBehavior.Cascade);
        });

        model.Entity<Factuurregel>(e =>
        {
            e.ToTable("Factuurregel");
            e.HasOne(r => r.BtwGroep).WithMany().HasForeignKey(r => r.BtwGroepId).OnDelete(DeleteBehavior.Restrict);
            e.HasOne<Artikel>().WithMany().HasForeignKey(r => r.ArtikelId).OnDelete(DeleteBehavior.SetNull);
        });

        model.Entity<Herinnering>().ToTable("Herinnering");

        model.Entity<Nummerreeks>(e =>
        {
            e.ToTable("Nummerreeks");
            e.HasKey(n => n.Jaar);
            e.Property(n => n.Jaar).ValueGeneratedNever();
        });

        model.Entity<Briefpapier>().ToTable("Briefpapier");

        model.Entity<Tekstsjabloon>(e =>
        {
            e.ToTable("Tekstsjabloon");
            e.HasIndex(t => new { t.Soort, t.Taal }).IsUnique();
        });
    }
}
