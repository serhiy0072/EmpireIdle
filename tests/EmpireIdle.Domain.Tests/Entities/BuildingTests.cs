using EmpireIdle.Domain.Entities;
using EmpireIdle.Domain.Exceptions;
using EmpireIdle.Domain.Services;
using EmpireIdle.Domain.ValueObjects;

namespace EmpireIdle.Domain.Tests.Entities;

public class BuildingTests
{
    // farm: 10/хв, кап 60, ріст капу 1.3
    private static readonly BuildingConfig Farm = TestData.FarmConfigs()["farm"];

    private static Building CreateFarm() => new(Guid.NewGuid(), Guid.NewGuid(), "farm", DateTime.UtcNow);

    /// <summary>Піднімає рівень через реальний шлях: почати → завершити.</summary>
    private static void RaiseLevel(Building building, int times, DateTime utcNow)
    {
        for (var i = 0; i < times; i++)
        {
            building.BeginUpgrade(Farm, TimeSpan.Zero, utcNow, ProductionBoost.None, locationMultiplier: 1.0);
            building.CompleteConstruction(utcNow);
        }
    }

    /// <summary>Місткість буфера лінійна: BaseStorage × рівень.</summary>
    [Theory]
    [InlineData(1, 60)]
    [InlineData(2, 120)]
    [InlineData(3, 180)]
    [InlineData(4, 240)]
    public void GetStorageCap_ShouldGrowLinearly_FromBaseAtLevelOne(int level, int expectedCap)
    {
        var building = CreateFarm();
        RaiseLevel(building, level - 1, building.LastAccruedAt);

        Assert.Equal(expectedCap, building.GetStorageCap(60));
    }

    /// <summary>Буфер — лінійна функція часу. Жодних тіків не потрібно.</summary>
    [Fact]
    public void StoredAt_ShouldAccrueLinearly()
    {
        var building = CreateFarm();
        var start = building.LastAccruedAt;

        Assert.Equal(50, building.StoredAt(Farm, start.AddMinutes(5), ProductionBoost.None, locationMultiplier: 1.0));
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

        Assert.Equal(5, building.StoredAt(Farm, start.AddSeconds(30), ProductionBoost.None, locationMultiplier: 1.0));
    }

    /// <summary>Понад кап нічого не накопичується — надлишок згорає.</summary>
    [Fact]
    public void StoredAt_ShouldCapAtStorageLimit()
    {
        var building = CreateFarm();
        var start = building.LastAccruedAt;

        Assert.Equal(60, building.StoredAt(Farm, start.AddHours(5), ProductionBoost.None, locationMultiplier: 1.0));
    }

    /// <summary>Рівень множить ставку: 2 рівень виробляє вдвічі швидше.</summary>
    [Fact]
    public void StoredAt_ShouldScaleWithLevel()
    {
        var building = CreateFarm();
        var start = building.LastAccruedAt;
        RaiseLevel(building, 1, start);

        // 2 рівень × 10/хв × 3 хв = 60, кап на 2 рівні 78 — не заважає
        Assert.Equal(60, building.StoredAt(Farm, start.AddMinutes(3), ProductionBoost.None, locationMultiplier: 1.0));
    }

    /// <summary>Буст множить лише той відрізок, коли він реально діяв.</summary>
    [Fact]
    public void StoredAt_ShouldApplyBoostOnlyWithinItsWindow()
    {
        var building = CreateFarm();
        var start = building.LastAccruedAt;

        // 2 хв ×2 = 40, далі 2 хв ×1 = 20
        var boost = new ProductionBoost(2.0, start, start.AddMinutes(2));

        Assert.Equal(60, building.StoredAt(Farm, start.AddMinutes(4), boost, locationMultiplier: 1.0));
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
        Assert.Equal(60, building.StoredAt(Farm, start.AddMinutes(5), boost, locationMultiplier: 1.0));
    }

    /// <summary>Буст, що скінчився до початку періоду, не впливає ні на що.</summary>
    [Fact]
    public void StoredAt_ShouldIgnoreBoostThatExpiredBeforePeriod()
    {
        var building = CreateFarm();
        var start = building.LastAccruedAt;

        var boost = new ProductionBoost(2.0, start.AddMinutes(-30), start.AddMinutes(-10));

        Assert.Equal(50, building.StoredAt(Farm, start.AddMinutes(5), boost, locationMultiplier: 1.0));
    }

    /// <summary>Під час будівництва виробництво зупинене.</summary>
    [Fact]
    public void StoredAt_ShouldFreezeDuringConstruction()
    {
        var building = CreateFarm();
        var start = building.LastAccruedAt;

        building.BeginUpgrade(Farm, TimeSpan.FromMinutes(10), start.AddMinutes(3), ProductionBoost.None, locationMultiplier: 1.0);

        // 30 накопичено до апгрейду; далі нічого, скільки б не минуло
        Assert.Equal(30, building.StoredAt(Farm, start.AddMinutes(9), ProductionBoost.None, locationMultiplier: 1.0));
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

        building.BeginUpgrade(Farm, TimeSpan.FromMinutes(10), start.AddMinutes(3), ProductionBoost.None, locationMultiplier: 1.0);
        var completedAt = start.AddMinutes(13);
        building.CompleteConstruction(completedAt);

        // 30 до апгрейду + 2 рівень × 10/хв × 1 хв = 20
        Assert.Equal(50, building.StoredAt(Farm, completedAt.AddMinutes(1), ProductionBoost.None, locationMultiplier: 1.0));
    }

    /// <summary>Матеріалізація фіксує накопичене й зсуває мітку часу.</summary>
    [Fact]
    public void Materialize_ShouldBankProductionAndMoveMarker()
    {
        var building = CreateFarm();
        var start = building.LastAccruedAt;
        var at = start.AddMinutes(4);

        building.Materialize(Farm, at, ProductionBoost.None, locationMultiplier: 1.0);

        Assert.Equal(40, building.AccruedAmount);
        Assert.Equal(at, building.LastAccruedAt);
    }

    /// <summary>Збір повертає накопичене й обнуляє буфер.</summary>
    [Fact]
    public void Collect_ShouldReturnBufferAndReset()
    {
        var building = CreateFarm();
        var at = building.LastAccruedAt.AddMinutes(5);

        var collected = building.Collect(Farm, at, ProductionBoost.None, locationMultiplier: 1.0);

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

        building.Collect(Farm, at, ProductionBoost.None, locationMultiplier: 1.0);

        Assert.Equal(0, building.Collect(Farm, at, ProductionBoost.None, locationMultiplier: 1.0));
    }

    /// <summary>Невиробнича будівля нічого не накопичує.</summary>
    [Fact]
    public void StoredAt_ShouldReturnZero_ForNonProducingBuilding()
    {
        var config = new BuildingConfig { Key = "townhall", ProducesResource = null, BaseStorage = 0 };
        var building = new Building(Guid.NewGuid(), Guid.NewGuid(), "townhall", DateTime.UtcNow);

        Assert.Equal(0, building.StoredAt(config, building.LastAccruedAt.AddHours(10), ProductionBoost.None, locationMultiplier: 1.0));
    }

    /// <summary>Апгрейд не можна почати двічі.</summary>
    [Fact]
    public void BeginUpgrade_ShouldRejectSecondUpgrade()
    {
        var building = CreateFarm();
        var now = building.LastAccruedAt;

        building.BeginUpgrade(Farm, TimeSpan.FromMinutes(10), now, ProductionBoost.None, locationMultiplier: 1.0);

        Assert.Throws<InvalidStateException>(() =>
            building.BeginUpgrade(Farm, TimeSpan.FromMinutes(10), now, ProductionBoost.None, locationMultiplier: 1.0));
    }

    /// <summary>
    /// Лінійний ріст переповнює int лише на абсурдних числах, але кламп лишається:
    /// базова місткість приходить із конфіга, а його редагують руками.
    /// </summary>
    [Fact]
    public void GetStorageCap_ShouldClampInsteadOfOverflowing()
    {
        var building = CreateFarm();
        RaiseLevel(building, 99, building.LastAccruedAt);

        Assert.Equal(int.MaxValue, building.GetStorageCap(int.MaxValue / 10));
    }
}
