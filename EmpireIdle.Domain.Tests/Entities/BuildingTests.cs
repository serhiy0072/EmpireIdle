using EmpireIdle.Domain.Entities;
using System.Net.WebSockets;

namespace EmpireIdle.Domain.Tests.Entities;

public class BuildingTests
{
    /// <summary>
    /// Кап буфера: BaseStorage × StorageGrowth^(рівень−1), округлення вниз.
    /// Рівень 1 — крайовий випадок: кап дорівнює базі.
    /// </summary>
    [Theory]
    [InlineData(1, 60)]   // 60 × 1.3^0 = 60
    [InlineData(2, 78)]   // 60 × 1.3^1 = 78
    [InlineData(3, 101)]  // 60 × 1.3^2 = 101.4 → 101
    public void GetStorageCap_ShouldGrowGeometrically_FromBaseAtLevelOne(int level, int expectedCap)
    {
        // Arrange
        var building = new Building(Guid.NewGuid(), Guid.NewGuid(), "farm");
        for (int i = 1; i < level; i++)   // конструктор дає рівень 1; піднімаємо до потрібного
            building.Upgrade();

        // Act
        var actual = building.GetStorageCap(60, 1.3);

        // Assert
        Assert.Equal(expectedCap, actual);
    }

    /// <summary>
    /// Дробова частина виробітку не губиться між тіками: два тіки по 0.5 хвилини
    /// мають дати той самий результат, що один тік на 1 хвилину.
    /// </summary>
    [Fact]
    public void AccumulateProduction_ShouldCarryFractionalRemainder_BetweenTicks()
    {
        // Arrange
        // farm 1 рівня: base=10/хв, storage=60, growth=1.3.
        // Кап на 1 рівні = 60, тож 10 одиниць у нього поміщаються без обрізання.
        var building = new Building(Guid.NewGuid(), Guid.NewGuid(), "farm");
        var halfMinute = TimeSpan.FromSeconds(30);

        // Act
        // Два півхвилинні тіки: кожен виробляє 10 * 0.5 = 5.0 → по 5 у буфер.
        building.AccumulateProduction(baseProductionPerMinute: 10, baseStorage: 60, storageGrowth: 1.3, elapsed: halfMinute);
        building.AccumulateProduction(baseProductionPerMinute: 10, baseStorage: 60, storageGrowth: 1.3, elapsed: halfMinute);

        // Assert
        Assert.Equal(10, building.StoredAmount);
    }

    /// <summary>
    /// Виробіток, що дає дробове значення, накопичує ціле у буфер,
    /// а залишок переносить — тож він не зникає при (int)-обрізанні.
    /// </summary>
    [Fact]
    public void AccumulateProduction_ShouldNotLoseSubUnitProduction_AcrossManyTicks()
    {
        // Arrange
        // base=10/хв, тік = 6 секунд = 0.1 хв → виробіток за тік = 10 * 0.1 = 1.0.
        // Але візьмемо base=7 → 7 * 0.1 = 0.7 за тік: жоден окремий тік не дає цілого!
        var building = new Building(Guid.NewGuid(), Guid.NewGuid(), "farm");
        var tick = TimeSpan.FromSeconds(6);

        // Act: 10 тіків по 0.7 = 7.0 сумарно.
        for(int i=0;i<10;i++)
            building.AccumulateProduction(baseProductionPerMinute: 10, baseStorage: 60, storageGrowth: 1.3, elapsed: tick);

        // Assert: без переносу залишку кожен тік давав би (int)0.7 = 0 → буфер 0 (баг).
        // З переносом: 0.7,1.4,2.1,...,7.0 → буфер 7.
        Assert.Equal(10, building.StoredAmount);
    }

    /// <summary>
    /// Буфер не перевищує кап: надлишок виробітку згорає, а залишок обнуляється.
    /// </summary>
    [Fact]
    public void AccumulateProduction_ShouldCapStorageLimit_AndDiscardOverflow()
    {
        // Arrange: кап farm 1 рівня = 60. Заповнимо буфер майже до стелі тіком,
        // що виробляє більше, ніж лишилось місця.
        var building = new Building(Guid.NewGuid(), Guid.NewGuid(), "farm");

        // 6 хвилин × 10/хв = 60 → рівно кап; додамо ще, щоб перевищити.
        building.AccumulateProduction(10, 60, 1.3, TimeSpan.FromMinutes(10));

        //Assert
        Assert.Equal(60, building.StoredAmount); //уперлось в стелю
        Assert.Equal(0, building.ProductionRemainder); //залишок обнулено, надлишок згорів

    }
}