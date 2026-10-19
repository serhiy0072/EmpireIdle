using EmpireIdle.Application.Common.Services;
using EmpireIdle.Application.Interfaces;
using EmpireIdle.Application.Rewards.Contracts;
using EmpireIdle.Application.Rewards.Granters;
using EmpireIdle.Domain.Entities;
using EmpireIdle.Domain.Services;
using EmpireIdle.Domain.Services.Config;
using EmpireIdle.TestKit;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Time.Testing;
using NSubstitute;

namespace EmpireIdle.Application.Tests.Rewards;

/// <summary>Випадковий артефакт рідкості: кожен кидок — окремий предмет набору відкритого данжу цієї рідкості.</summary>
public class ArtifactRewardGranterTests
{
    private static readonly Guid PlayerId = Guid.NewGuid();

    private readonly IInventoryRepository _inventory = Substitute.For<IInventoryRepository>();
    private readonly IVillageRepository _villages = Substitute.For<IVillageRepository>();
    private readonly ArtifactRewardGranter _granter;

    public ArtifactRewardGranterTests()
    {
        var catalog = new GameConfigBuilder().WithBuildings().WithHeroes().WithEquipment().WithDungeons().BuildCatalog();

        var serverContext = Substitute.For<IServerContext>();
        serverContext.ServerId.Returns(1);

        var random = Substitute.For<IRandomSource>();
        random.Next(Arg.Any<int>()).Returns(0);

        _villages.GetByPlayerIdReadOnlyAsync(PlayerId, Arg.Any<CancellationToken>()).Returns(Entities.VillageWithTownhall());

        _granter = new ArtifactRewardGranter(new ItemGranter(_inventory, serverContext), _villages, catalog, random,
            new FakeTimeProvider(Entities.Now), NullLogger<ArtifactRewardGranter>.Instance);
    }

    private static RewardContext Context(string? rarity, int amount)
        => new(PlayerId, new RewardConfig { Type = "Artifact", Key = rarity, Amount = amount }, "tower:10", Entities.Now);

    [Fact]
    public async Task GrantAsync_ShouldGrantOneArtifactOfTheRarity_PerAmount()
    {
        await _granter.GrantAsync(Context("Rare", 2), CancellationToken.None);

        await _inventory.Received(2).AddEquipmentAsync(
            Arg.Is<EquipmentItem>(e => e.ItemKey.StartsWith($"{TestKeys.DungeonSetKey}_rare_")), Arg.Any<CancellationToken>());
    }

    [Theory]
    [InlineData("Legendary", 1)]
    [InlineData(null, 1)]
    [InlineData("Common", 0)]
    public async Task GrantAsync_ShouldReject_ABrokenReward(string? rarity, int amount)
    {
        await Assert.ThrowsAsync<InvalidOperationException>(() => _granter.GrantAsync(Context(rarity, amount), CancellationToken.None));

        await _inventory.DidNotReceive().AddEquipmentAsync(Arg.Any<EquipmentItem>(), Arg.Any<CancellationToken>());
    }
}
