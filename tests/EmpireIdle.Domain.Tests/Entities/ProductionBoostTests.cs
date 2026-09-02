using EmpireIdle.Domain.ValueObjects;

namespace EmpireIdle.Domain.Tests.ValueObjects;

/// <summary>
/// Перетин вікна буста з періодом накопичення — основа розрахунку буфера.
/// Помилка тут дає або втрачений виробіток, або безкоштовний множник.
/// </summary>
public class ProductionBoostTests
{
    private static readonly DateTime Base = new(2026, 1, 1, 12, 0, 0, DateTimeKind.Utc);

    [Fact]
    public void OverlapMinutes_ShouldReturnFullPeriod_WhenBoostCoversIt()
    {
        var boost = new ProductionBoost(2.0, Base.AddMinutes(-10), Base.AddMinutes(30));

        Assert.Equal(10, boost.OverlapMinutes(Base, Base.AddMinutes(10)));
    }

    [Fact]
    public void OverlapMinutes_ShouldClipToBoostEnd_WhenBoostExpiresInside()
    {
        var boost = new ProductionBoost(2.0, Base.AddMinutes(-10), Base.AddMinutes(4));

        Assert.Equal(4, boost.OverlapMinutes(Base, Base.AddMinutes(10)));
    }

    [Fact]
    public void OverlapMinutes_ShouldClipToBoostStart_WhenBoostStartsInside()
    {
        var boost = new ProductionBoost(2.0, Base.AddMinutes(6), Base.AddMinutes(60));

        Assert.Equal(4, boost.OverlapMinutes(Base, Base.AddMinutes(10)));
    }

    [Fact]
    public void OverlapMinutes_ShouldReturnZero_WhenBoostEndedBeforePeriod()
    {
        var boost = new ProductionBoost(2.0, Base.AddMinutes(-60), Base.AddMinutes(-10));

        Assert.Equal(0, boost.OverlapMinutes(Base, Base.AddMinutes(10)));
    }

    [Fact]
    public void OverlapMinutes_ShouldReturnZero_WhenBoostStartsAfterPeriod()
    {
        var boost = new ProductionBoost(2.0, Base.AddMinutes(20), Base.AddMinutes(60));

        Assert.Equal(0, boost.OverlapMinutes(Base, Base.AddMinutes(10)));
    }

    [Fact]
    public void None_ShouldNeverOverlap()
    {
        Assert.Equal(0, ProductionBoost.None.OverlapMinutes(Base, Base.AddMinutes(10)));
    }
}
