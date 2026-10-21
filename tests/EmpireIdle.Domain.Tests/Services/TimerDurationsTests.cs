using EmpireIdle.Domain.Services;
using EmpireIdle.Domain.Services.Config;
using EmpireIdle.Domain.ValueObjects;

namespace EmpireIdle.Domain.Tests.Services;

/// <summary>
/// Одна формула на таймер і на все, що від нього рахує. Кланова допомога скорочує частку
/// повного часу — якщо її «повний час» не збігається з реальним таймером, вона обіцяє більше,
/// ніж дає.
/// </summary>
public class TimerDurationsTests
{
    private static readonly DateTime Now = new(2026, 9, 27, 12, 0, 0, DateTimeKind.Utc);

    /// <summary>
    /// Рання будівля: множник ранніх рівнів (×3 на рівні 1) — частина таймера, і тривалість
    /// для допомоги мусить його містити. Звіряємо з таймером, який справді запустив апгрейд.
    /// </summary>
    [Fact]
    public void Construction_ShouldMatchTheTimerTheUpgradeActuallyStarts()
    {
        var configs = TestKit.Entities.FarmConfigs();
        var village = TestKit.Entities.VillageWithTownhall(townhallLevel: 5, resourceAmount: 100_000);
        var farm = village.Buildings.Single(b => b.Type == TestKit.TestKeys.Farm);

        var expected = TimerDurations.Construction(configs[TestKit.TestKeys.Farm], farm.Level.Value);

        village.BeginBuildingUpgrade(farm.Id, configs, Now, ProductionBoost.None,
            mainBuildingKey: TestKit.TestKeys.Townhall, serverLevel: 99, levelsPerTier: 10, maxBuildingLevel: 30, locationMultiplier: 1.0);

        Assert.Equal(Now + expected, farm.ConstructionCompletesAt);

        // Саме те, чого бракувало допомозі: множник ранніх рівнів робить таймер довшим за просту криву
        Assert.True(expected > TimeSpan.FromMinutes(configs[TestKit.TestKeys.Farm].BaseBuildMinutes));
    }

    /// <summary>Тренування на рівень 3 — сума кроків 1+2+3 за кривою часу, а не ціни.</summary>
    [Fact]
    public void Training_ShouldSumTheStepsUpToTheTargetLevel()
    {
        var unit = new UnitConfig { Key = "infantry", BaseTrainSeconds = 10, TrainTimeGrowth = 2.0, LevelUpCostGrowth = 3.0 };

        // Кроки: 10 + 20 + 40 = 70 с на штуку, партія з двох — 140
        Assert.Equal(TimeSpan.FromSeconds(140), TimerDurations.Training(unit, level: 3, count: 2));
    }

    /// <summary>
    /// Числа рішення 13.10.2026: партія з 150 юнітів 1 рівня — 10 хвилин, 10 рівня — 2,5 години.
    /// </summary>
    [Theory]
    [InlineData(1, 10)]
    [InlineData(10, 150)]
    public void Training_ShouldTakeTenMinutesAtLevelOne_AndTwoAndAHalfHoursAtLevelTen(int level, int minutes)
    {
        var unit = new UnitConfig { Key = "infantry", BaseTrainSeconds = 4, TrainTimeGrowth = 1.0875 };

        Assert.Equal(TimeSpan.FromMinutes(minutes), TimerDurations.Training(unit, level, count: 150));
    }
}
