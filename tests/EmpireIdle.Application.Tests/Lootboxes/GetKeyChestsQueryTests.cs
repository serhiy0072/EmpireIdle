using EmpireIdle.Application.Interfaces;
using EmpireIdle.Application.Lootboxes.Contracts;
using EmpireIdle.Application.Lootboxes.Queries;
using EmpireIdle.Domain.Entities;
using EmpireIdle.Domain.Services;
using EmpireIdle.Domain.Services.Config;
using EmpireIdle.TestKit;
using Microsoft.Extensions.Time.Testing;
using NSubstitute;

namespace EmpireIdle.Application.Tests.Lootboxes;

/// <summary>Вітрина ключових скринь: ключі в рюкзаку, залишок безкоштовних і момент наступного.</summary>
public class GetKeyChestsQueryTests
{
    private static readonly DateTime Now = new(2026, 10, 12, 12, 0, 0, DateTimeKind.Utc);
    private static readonly Guid PlayerId = Guid.NewGuid();
    private const string Key = "key_small";

    private readonly ILootboxRepository _lootboxes = Substitute.For<ILootboxRepository>();
    private readonly IInventoryRepository _inventory = Substitute.For<IInventoryRepository>();
    private readonly GameConfig _config = new GameConfigBuilder().WithLootboxes().Build();

    public GetKeyChestsQueryTests()
    {
        _config.Items.Add(new ItemConfig { Key = Key, Type = "chestkey", DisplayName = "Ключ", Description = "k" });
        _config.KeyChests = [new KeyChestConfig { LootboxKey = TestKeys.SmallChest, KeyItemKey = Key, FreePerDay = 5, FreeCooldownMinutes = 5 }];

        _lootboxes.GetKeyChestProgressesReadOnlyAsync(PlayerId, Arg.Any<CancellationToken>()).Returns([]);
        _inventory.GetItemsAsync(PlayerId, Arg.Any<CancellationToken>()).Returns([]);
    }

    private async Task<KeyChestView> Single()
        => Assert.Single(await new GetKeyChestsQueryHandler(_lootboxes, _inventory, new GameCatalog(_config), new FakeTimeProvider(Now))
            .Handle(new GetKeyChestsQuery(PlayerId), CancellationToken.None));

    /// <summary>Скриню ще не відкривали — усі безкоштовні на місці, відкрити можна зараз.</summary>
    [Fact]
    public async Task Handle_ShouldOfferEveryFreeOpening_ForAChestNeverOpened()
    {
        var view = await Single();

        Assert.Equal((5, 5, (DateTime?)null, 0), (view.FreePerDay, view.FreeLeftToday, view.NextFreeAt, view.Keys));
    }

    /// <summary>Після відкриття — на одне менше й таймер перезарядки; ключі — з рюкзака.</summary>
    [Fact]
    public async Task Handle_ShouldCountDown_AfterAFreeOpening()
    {
        var progress = new KeyChestProgress(Guid.NewGuid(), PlayerId, TestKeys.SmallChest);
        progress.UseFree(5, 5, Now.AddMinutes(-1));
        _lootboxes.GetKeyChestProgressesReadOnlyAsync(PlayerId, Arg.Any<CancellationToken>()).Returns([progress]);
        _inventory.GetItemsAsync(PlayerId, Arg.Any<CancellationToken>()).Returns([new PlayerItem(Guid.NewGuid(), PlayerId, Key, 7)]);

        var view = await Single();

        Assert.Equal((4, Now.AddMinutes(4), 7), (view.FreeLeftToday, view.NextFreeAt, view.Keys));
    }
}
