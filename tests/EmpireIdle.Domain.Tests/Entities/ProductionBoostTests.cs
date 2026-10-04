using EmpireIdle.Domain.ValueObjects;

namespace EmpireIdle.Domain.Tests.ValueObjects;

/// <summary>
/// Перетин вікон буста з періодом накопичення — основа розрахунку буфера.
/// Помилка тут дає або втрачений виробіток, або безкоштовний множник.
/// </summary>
public class ProductionBoostTests
{
    private static readonly DateTime Base = new(2026, 1, 1, 12, 0, 0, DateTimeKind.Utc);

    private static BoostWindow Window(double bonus, int startMinutes, int endMinutes)
        => new(bonus, Base.AddMinutes(startMinutes), Base.AddMinutes(endMinutes));

    [Fact]
    public void OverlapMinutes_ShouldReturnFullPeriod_WhenTheWindowCoversIt()
        => Assert.Equal(10, Window(1, -10, 30).OverlapMinutes(Base, Base.AddMinutes(10)));

    [Fact]
    public void OverlapMinutes_ShouldClipToTheWindowEnd_WhenItExpiresInside()
        => Assert.Equal(4, Window(1, -10, 4).OverlapMinutes(Base, Base.AddMinutes(10)));

    [Fact]
    public void OverlapMinutes_ShouldClipToTheWindowStart_WhenItStartsInside()
        => Assert.Equal(4, Window(1, 6, 60).OverlapMinutes(Base, Base.AddMinutes(10)));

    [Fact]
    public void OverlapMinutes_ShouldReturnZero_WhenTheWindowEndedBeforeThePeriod()
        => Assert.Equal(0, Window(1, -60, -10).OverlapMinutes(Base, Base.AddMinutes(10)));

    [Fact]
    public void OverlapMinutes_ShouldReturnZero_WhenTheWindowStartsAfterThePeriod()
        => Assert.Equal(0, Window(1, 20, 60).OverlapMinutes(Base, Base.AddMinutes(10)));

    /// <summary>Множник ×2 — надбавка 1.0: за 10 хвилин перекриття це 10 «зайвих» хвилин виробітку.</summary>
    [Fact]
    public void BonusMinutes_ShouldTurnAMultiplierIntoItsBonus()
        => Assert.Equal(10, new ProductionBoost(2.0, Base, Base.AddMinutes(30)).BonusMinutes(Base, Base.AddMinutes(10)), precision: 10);

    /// <summary>Буст крамниці й звір складаються додаванням: ×2 і +15% на тих самих хвилинах — це ×2.15.</summary>
    [Fact]
    public void BonusMinutes_ShouldAddWindowsUp()
    {
        var boost = new ProductionBoost(2.0, Base, Base.AddMinutes(10)).With(Window(0.15, 0, 10));

        Assert.Equal(11.5, boost.BonusMinutes(Base, Base.AddMinutes(10)), precision: 10);
    }

    [Fact]
    public void None_ShouldGiveNoBonus()
        => Assert.Equal(0, ProductionBoost.None.BonusMinutes(Base, Base.AddMinutes(10)));
}
