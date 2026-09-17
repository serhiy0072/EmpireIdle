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

public class EquipmentRewardGranterTests
{
    private readonly IInventoryRepository _inventory = Substitute.For<IInventoryRepository>();
    private readonly EquipmentRewardGranter _granter;

    public EquipmentRewardGranterTests()
    {
        var catalog = new GameConfigBuilder().WithHeroes().WithEquipment().BuildCatalog();

        var serverContext = Substitute.For<IServerContext>();
        serverContext.ServerId.Returns(1);

        var random = Substitute.For<IRandomSource>();
        random.Next(Arg.Any<int>()).Returns(42);

        var itemGranter = new ItemGranter(_inventory, serverContext, random,
            new ArtifactRoller(catalog.Config.Equipment));

        _granter = new EquipmentRewardGranter(itemGranter, catalog,
            new FakeTimeProvider(TestKit.Entities.Now), NullLogger<EquipmentRewardGranter>.Instance);
    }

    // Зброя: ролер не задіяний, тест перевіряє лише кількість екземплярів
    private static RewardContext Context(int amount) => new(
        Guid.NewGuid(),
        new RewardConfig { Type = "Equipment", Key = TestKeys.Weapon, Amount = amount },
        "test_quest",
        TestKit.Entities.Now);

    [Fact]
    public async Task GrantAsync_ShouldCreateOneInstancePerAmount()
    {
        await _granter.GrantAsync(Context(3), CancellationToken.None);

        await _inventory.Received(3).AddEquipmentAsync(Arg.Any<EquipmentItem>(), Arg.Any<CancellationToken>());
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public async Task GrantAsync_ShouldReject_NonPositiveAmount(int amount)
    {
        await Assert.ThrowsAsync<InvalidOperationException>(
            () => _granter.GrantAsync(Context(amount), CancellationToken.None));

        await _inventory.DidNotReceive().AddEquipmentAsync(Arg.Any<EquipmentItem>(), Arg.Any<CancellationToken>());
    }
}
