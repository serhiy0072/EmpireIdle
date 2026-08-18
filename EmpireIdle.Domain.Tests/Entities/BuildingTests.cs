using EmpireIdle.Domain.Entities;
using EmpireIdle.Domain.Services;
using EmpireIdle.Domain.ValueObjects;

namespace EmpireIdle.Domain.Tests.Entities;

public class BuildingTests
{
    // farm: 10/хв, кап 60, ріст капу 1.3
    private static readonly BuildingConfig Farm = TestData.FarmConfigs()["farm"];

    private static Building CreateFarm() => new(Guid.NewGuid(), Guid.NewGuid(), "farm");

    /// <summary>Піднімає рівень через реальний шлях: почати → завершити.</summary>
    private static void RaiseLevel(Building building, int times, DateTime utcNow)
    {
        for (var i = 0; i < times; i++)
        {
            building.BeginUpgrade(Farm, TimeSpan.Zero, utcNow, ProductionBoost.None);
            building.CompleteConstruction(utcNow);
        }
    }

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
        var building = CreateFarm();
        RaiseLevel(building, level - 1, building.LastAccruedAt);

        Assert.Equal(expectedCap, building.GetStorageCap(60, 1.3));
    }

    /// <summary>Буфер — лінійна функція часу. Жодних тіків не потрібно.</summary>
    [Fact]
    public void StoredAt_ShouldAccrueLinearly()
    {
        var building = CreateFarm();
        var start = building.LastAccruedAt;

        Assert.Equal(50, building.StoredAt(Farm, start.AddMinutes(5), ProductionBoost.None));
    }

    /// <summary>
    /// Дрібний виробіток не губиться: обчислення одне від мітки часу,
    /// а не сотні (int)-обрізань щохвилини.
    /// </summary>
    [Fact]
    public void StoredAt_ShouldNotLoseSubUnitProduction()
    {
        var building = CreateFarm();
        var start = building.LastAccruedAt;

        Assert.Equal(5, building.StoredAt(Farm, start.AddSeconds(30), ProductionBoost.None));
    }

    /// <summary>Понад кап нічого не накопичується — надлишок згорає.</summary>
    [Fact]
    public void StoredAt_ShouldCapAtStorageLimit()
    {
        var building = CreateFarm();
        var start = building.LastAccruedAt;

        Assert.Equal(60, building.StoredAt(Farm, start.AddHours(5), ProductionBoost.None));
    }

    /// <summary>Рівень множить ставку: 2 рівень виробляє вдвічі швидше.</summary>
    [Fact]
    public void StoredAt_ShouldScaleWithLevel()
    {
        var building = CreateFarm();
        var start = building.LastAccruedAt;
        RaiseLevel(building, 1, start);

        // 2 рівень × 10/хв × 3 хв = 60, кап на 2 рівні 78 — не заважає
        Assert.Equal(60, building.StoredAt(Farm, start.AddMinutes(3), ProductionBoost.None));
    }

    /// <summary>Буст множить лише той відрізок, коли він реально діяв.</summary>
    [Fact]
    public void StoredAt_ShouldApplyBoostOnlyWithinItsWindow()
    {
        var building = CreateFarm();
        var start = building.LastAccruedAt;

        // 2 хв ×2 = 40, далі 2 хв ×1 = 20
        var boost = new ProductionBoost(2.0, start, start.AddMinutes(2));

        Assert.Equal(60, building.StoredAt(Farm, start.AddMinutes(4), boost));
    }

    /// <summary>
    /// ГОЛОВНИЙ ТЕСТ ПРОТИ ЕКСПЛОЙТУ: буст, увімкнений у кінці періоду,
    /// не множить весь накопичений раніше виробіток.
    /// </summary>
    [Fact]
    public void StoredAt_ShouldNotApplyBoostRetroactively()
    {
        var building = CreateFarm();
        var start = building.LastAccruedAt;

        // Чекали 4 хв, на 4-й хвилині увімкнули ×2 і збираємо на 5-й
        var boost = new ProductionBoost(2.0, start.AddMinutes(4), start.AddMinutes(64));

        // 4 хв ×1 = 40, 1 хв ×2 = 20 → 60, а не 100
        Assert.Equal(60, building.StoredAt(Farm, start.AddMinutes(5), boost));
    }

    /// <summary>Буст, що скінчився до початку періоду, не впливає ні на що.</summary>
    [Fact]
    public void StoredAt_ShouldIgnoreBoostThatExpiredBeforePeriod()
    {
        var building = CreateFarm();
        var start = building.LastAccruedAt;

        var boost = new ProductionBoost(2.0, start.AddMinutes(-30), start.AddMinutes(-10));

        Assert.Equal(50, building.StoredAt(Farm, start.AddMinutes(5), boost));
    }

    /// <summary>Під час будівництва виробництво зупинене.</summary>
    [Fact]
    public void StoredAt_ShouldFreezeDuringConstruction()
    {
        var building = CreateFarm();
        var start = building.LastAccruedAt;

        building.BeginUpgrade(Farm, TimeSpan.FromMinutes(10), start.AddMinutes(3), ProductionBoost.None);

        // 30 накопичено до апгрейду; далі нічого, скільки б не минуло
        Assert.Equal(30, building.StoredAt(Farm, start.AddMinutes(9), ProductionBoost.None));
    }

    /// <summary>
    /// Період будівництва не зараховується як виробіток —
    /// і тим паче не за новою, вищою ставкою.
    /// </summary>
    [Fact]
    public void CompleteConstruction_ShouldNotCreditTheConstructionPeriod()
    {
        var building = CreateFarm();
        var start = building.LastAccruedAt;

        building.BeginUpgrade(Farm, TimeSpan.FromMinutes(10), start.AddMinutes(3), ProductionBoost.None);
        var completedAt = start.AddMinutes(13);
        building.CompleteConstruction(completedAt);

        // 30 до апгрейду + 2 рівень × 10/хв × 1 хв = 20
        Assert.Equal(50, building.StoredAt(Farm, completedAt.AddMinutes(1), ProductionBoost.None));
    }

    /// <summary>Матеріалізація фіксує накопичене й зсуває мітку часу.</summary>
    [Fact]
    public void Materialize_ShouldBankProductionAndMoveMarker()
    {
        var building = CreateFarm();
        var start = building.LastAccruedAt;
        var at = start.AddMinutes(4);

        building.Materialize(Farm, at, ProductionBoost.None);

        Assert.Equal(40, building.AccruedAmount);
        Assert.Equal(at, building.LastAccruedAt);
    }

    /// <summary>Збір повертає накопичене й обнуляє буфер.</summary>
    [Fact]
    public void Collect_ShouldReturnBufferAndReset()
    {
        var building = CreateFarm();
        var at = building.LastAccruedAt.AddMinutes(5);

        var collected = building.Collect(Farm, at, ProductionBoost.None);

        Assert.Equal(50, collected);
        Assert.Equal(0, building.AccruedAmount);
        Assert.Equal(at, building.LastCollectedAt);
    }

    /// <summary>Повторний збір тієї ж миті дає нуль — подвійного нарахування немає.</summary>
    [Fact]
    public void Collect_ShouldReturnZero_WhenCalledTwiceAtTheSameMoment()
    {
        var building = CreateFarm();
        var at = building.LastAccruedAt.AddMinutes(5);

        building.Collect(Farm, at, ProductionBoost.None);

        Assert.Equal(0, building.Collect(Farm, at, ProductionBoost.None));
    }

    /// <summary>Невиробнича будівля нічого не накопичує.</summary>
    [Fact]
    public void StoredAt_ShouldReturnZero_ForNonProducingBuilding()
    {
        var config = new BuildingConfig { Key = "townhall", ProducesResource = null, BaseStorage = 0, StorageGrowth = 1.0 };
        var building = new Building(Guid.NewGuid(), Guid.NewGuid(), "townhall");

        Assert.Equal(0, building.StoredAt(config, building.LastAccruedAt.AddHours(10), ProductionBoost.None));
    }

    /// <summary>Апгрейд не можна почати двічі.</summary>
    [Fact]
    public void BeginUpgrade_ShouldRejectSecondUpgrade()
    {
        var building = CreateFarm();
        var now = building.LastAccruedAt;

        building.BeginUpgrade(Farm, TimeSpan.FromMinutes(10), now, ProductionBoost.None);

        Assert.Throws<InvalidOperationException>(() =>
            building.BeginUpgrade(Farm, TimeSpan.FromMinutes(10), now, ProductionBoost.None));
    }

    /// <summary>
    /// На високих рівнях геометричний кап виходить за межі int.
    /// Без обрізання каст дав би від'ємне число, і буфер завмер би назавжди.
    /// </summary>
    [Fact]
    public void GetStorageCap_ShouldClampInsteadOfOverflowing()
    {
        var building = CreateFarm();
        RaiseLevel(building, 99, building.LastAccruedAt);

        Assert.Equal(int.MaxValue, building.GetStorageCap(60, 1.3));
    }
}
