using EmpireIdle.Domain.Entities;

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
}