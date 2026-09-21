using EmpireIdle.Application.Garrisons.Queries;
using EmpireIdle.Application.Interfaces;
using EmpireIdle.Domain.Entities;
using EmpireIdle.Domain.Services;
using EmpireIdle.Domain.Services.Config;
using Microsoft.Extensions.Time.Testing;
using NSubstitute;

namespace EmpireIdle.Application.Tests.Garrisons;

/// <summary>
/// Клієнт показує вартість прискорення тренування ще до кліку — той самий
/// принцип, що й для будівель (SpeedUpCalculator рахує його заздалегідь).
/// </summary>
public class GetGarrisonQueryTests
{
    private static readonly DateTime Now = new(2026, 6, 1, 12, 0, 0, DateTimeKind.Utc);
    private static readonly Guid PlayerId = Guid.NewGuid();

    private readonly IVillageRepository _villages = Substitute.For<IVillageRepository>();
    private readonly IGarrisonRepository _garrisons = Substitute.For<IGarrisonRepository>();

    private static GameConfig Config() => new()
    {
        Buildings = [new BuildingConfig { Key = "townhall", IsMainBuilding = true, UpgradeCostGrowth = 1.45 }],
        Monetization = new MonetizationConfig
        {
            InstantFinishThresholdMinutes = 5,
            SpeedUpFactor = 2.0,
            SpeedUpExponent = 0.75
        }
    };

    private static GameCatalog Catalog() => new(Config());
    private static SpeedUpCalculator Calculator() => new(Config().Monetization);

    private GetGarrisonQueryHandler Handler() => new(
        _villages, _garrisons, Catalog(), new FakeTimeProvider(Now), Calculator());

    private (Village village, Garrison garrison) GivenVillageWithGarrison()
    {
        var village = new Village(Guid.NewGuid(), PlayerId, "Test", [], 0, 0);
        var garrison = new Garrison(Guid.NewGuid(), village.Id, village.ServerId);

        _villages.GetByPlayerIdAsync(PlayerId, Arg.Any<CancellationToken>()).Returns(village);
        _garrisons.GetByVillageIdReadOnlyAsync(village.Id, Arg.Any<CancellationToken>()).Returns(garrison);

        return (village, garrison);
    }

    /// <summary>Довга черга отримує ту саму ціну, що й SpeedUpCalculator напряму.</summary>
    [Fact]
    public async Task Handle_ShouldPriceATrainingOrder_ByRemainingTime()
    {
        var (_, garrison) = GivenVillageWithGarrison();
        garrison.TrainUnits("infantry", level: 1, count: 5, maxBatchSize: 100, armyCapacity: 1000,
            trainDuration: TimeSpan.FromMinutes(120), utcNow: Now);

        var order = garrison.TrainingOrders.Single();
        var expected = Calculator().GetInstantFinishCost(order.CompletesAt, Now);

        var response = await Handler().Handle(new GetGarrisonQuery(PlayerId), CancellationToken.None);

        var orderView = response.TrainingOrders.Single();
        Assert.True(expected > 0, "120 хвилин мають коштувати gems, інакше тест нічого не перевіряє.");
        Assert.Equal(expected, orderView.SpeedUpCostGems);
    }

    /// <summary>Черга коротша за безкоштовний поріг — ціна нульова.</summary>
    [Fact]
    public async Task Handle_ShouldPriceZero_BelowTheFreeThreshold()
    {
        var (_, garrison) = GivenVillageWithGarrison();
        garrison.TrainUnits("infantry", level: 1, count: 1, maxBatchSize: 100, armyCapacity: 1000,
            trainDuration: TimeSpan.FromMinutes(2), utcNow: Now);

        var response = await Handler().Handle(new GetGarrisonQuery(PlayerId), CancellationToken.None);

        var orderView = response.TrainingOrders.Single();
        Assert.Equal(0, orderView.SpeedUpCostGems);
    }
}
