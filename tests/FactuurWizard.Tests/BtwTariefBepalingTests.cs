using FactuurWizard.Core.Model;
using FactuurWizard.Core.Services;

namespace FactuurWizard.Tests;

public class BtwTariefBepalingTests
{
    private static readonly BtwGroep Laag = new()
    {
        Naam = "Laag",
        Tarieven =
        [
            new() { Percentage = 6m, Ingangsdatum = new(2001, 1, 1), Einddatum = new(2018, 12, 31) },
            new() { Percentage = 9m, Ingangsdatum = new(2019, 1, 1) },
        ],
    };

    [Fact]
    public void Tarief_volgt_de_datum()
    {
        Assert.Equal(6m, BtwTariefBepaling.PercentageOp(Laag, new(2018, 12, 31)));
        Assert.Equal(9m, BtwTariefBepaling.PercentageOp(Laag, new(2019, 1, 1)));
    }

    [Fact]
    public void Datum_zonder_tarief_geeft_fout()
    {
        Assert.Throws<InvalidOperationException>(() => BtwTariefBepaling.PercentageOp(Laag, new(2000, 6, 1)));
    }

    [Fact]
    public void Aansluitende_periodes_zijn_geldig()
    {
        Assert.Empty(BtwTariefBepaling.Valideer(Laag.Tarieven));
    }

    [Fact]
    public void Overlappende_periodes_zijn_ongeldig()
    {
        var tarieven = new List<BtwTarief>
        {
            new() { Percentage = 6m, Ingangsdatum = new(2001, 1, 1) },
            new() { Percentage = 9m, Ingangsdatum = new(2019, 1, 1) },
        };
        Assert.Single(BtwTariefBepaling.Valideer(tarieven));
    }
}
