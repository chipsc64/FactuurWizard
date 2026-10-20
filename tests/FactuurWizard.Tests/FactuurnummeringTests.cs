using FactuurWizard.Core.Services;

namespace FactuurWizard.Tests;

public class FactuurnummeringTests
{
    [Theory]
    [InlineData(null, 2026, 2026, 38, 38)] // eerste factuur in het eerste jaar: startnummer
    [InlineData(38, 2026, 2026, 38, 39)]   // daarna doortellen
    [InlineData(null, 2027, 2026, 38, 1)]  // nieuw jaar begint bij 1
    [InlineData(null, 2026, 2026, 0, 1)]   // startnummer minimaal 1
    public void Volgend_volgnummer(int? laatste, int jaar, int eersteJaar, int start, int verwacht)
    {
        Assert.Equal(verwacht, Factuurnummering.VolgendVolgnummer(laatste, jaar, eersteJaar, start));
    }

    [Fact]
    public void Formaat_is_jaar_met_vier_cijfers()
    {
        Assert.Equal("2026-0001", Factuurnummering.Formatteer(2026, 1));
        Assert.Equal("2026-0123", Factuurnummering.Formatteer(2026, 123));
    }
}
