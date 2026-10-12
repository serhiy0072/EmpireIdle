using EmpireIdle.Application.Interfaces;
using EmpireIdle.Application.Lootboxes.Commands;
using EmpireIdle.Application.Lootboxes.Contracts;
using EmpireIdle.Application.Lootboxes.Services;
using EmpireIdle.Application.Rewards;
using EmpireIdle.Application.Rewards.Contracts;
using EmpireIdle.Domain.Entities;
using EmpireIdle.Domain.Exceptions;
using EmpireIdle.Domain.Services;
using EmpireIdle.Domain.Services.Config;
using EmpireIdle.TestKit;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Time.Testing;
using NSubstitute;

namespace EmpireIdle.Application.Tests.Lootboxes;

/// <summary>
/// Ключова скриня (рішення 12.10.2026): безкоштовне відкриття за лімітом і перезарядкою
/// або ключами з рюкзака, по ключу за скриню.
/// </summary>
public class OpenKeyChestCommandTests
{
    private static readonly DateTime Now = new(2026, 10, 12, 12, 0, 0, DateTimeKind.Utc);
    private static readonly Guid PlayerId = Guid.NewGuid();
    private const string Key = "key_small";

    private readonly IInventoryRepository _inventory = Substitute.For<IInventoryRepository>();
    private readonly ILootboxRepository _lootboxes = Substitute.For<ILootboxRepository>();
    private readonly IVillageRepository _villages = Substitute.For<IVillageRepository>();
    private readonly IUnitOfWork _unitOfWork = Substitute.For<IUnitOfWork>();
    private readonly List<RewardConfig> _granted = [];
    private readonly List<KeyChestProgress> _added = [];
    private readonly GameConfig _config = new GameConfigBuilder().WithLootboxes().Build();

    public OpenKeyChestCommandTests()
    {
        // Мала скриня — одна позиція, щоб видача не залежала від сіда
        _config.Lootboxes.Single(b => b.Key == TestKeys.SmallChest).Entries.RemoveAll(e => e.Type != "Resource");
        _config.Items.Add(new ItemConfig { Key = Key, Type = "chestkey", DisplayName = "Ключ", Description = "k" });
        _config.KeyChests = [new KeyChestConfig { LootboxKey = TestKeys.SmallChest, KeyItemKey = Key, FreePerDay = 5, FreeCooldownMinutes = 5 }];

        _villages.GetByPlayerIdReadOnlyAsync(PlayerId, Arg.Any<CancellationToken>()).Returns(Entities.VillageWithTownhall());
        _lootboxes.AddKeyChestProgressAsync(Arg.Do<KeyChestProgress>(_added.Add), Arg.Any<CancellationToken>());
    }

    private PlayerItem GivenKeys(int count)
    {
        var stack = new PlayerItem(Guid.NewGuid(), PlayerId, Key, count);
        _inventory.GetItemAsync(PlayerId, Key, Arg.Any<CancellationToken>()).Returns(stack);
        return stack;
    }

    private Task<LootboxOpening> Open(bool free, int count = 1, string chest = TestKeys.SmallChest)
    {
        var granter = Substitute.For<IRewardGranter>();
        granter.RewardType.Returns("Resource");
        granter.GrantAsync(Arg.Do<RewardContext>(c => _granted.Add(c.Reward)), Arg.Any<CancellationToken>());

        var catalog = new GameCatalog(_config);
        var opener = new LootboxOpener(_lootboxes, _villages, new RewardDispatcher([granter]), catalog,
            Substitute.For<IRandomSource>());

        return new OpenKeyChestCommandHandler(_inventory, _lootboxes, opener, catalog, _unitOfWork, new FakeTimeProvider(Now),
                NullLogger<OpenKeyChestCommandHandler>.Instance)
            .Handle(new OpenKeyChestCommand(PlayerId, chest, free, count), CancellationToken.None);
    }

    /// <summary>Перше безкоштовне відкриття заводить лічильник і видає нагороду, ключів не чіпає.</summary>
    [Fact]
    public async Task Handle_ShouldOpenForFree_AndRecordTheOpening()
    {
        var keys = GivenKeys(3);

        var opening = await Open(free: true);

        Assert.Single(opening.Drops);
        var progress = Assert.Single(_added);
        Assert.Equal(1, progress.FreeUsedToday(Now));
        Assert.Equal(3, keys.Count);
        await _unitOfWork.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    /// <summary>Безкоштовне ще на перезарядці — відмова, нічого не видано.</summary>
    [Fact]
    public async Task Handle_ShouldRefuseAFreeOpening_BeforeTheCooldownEnds()
    {
        var progress = new KeyChestProgress(Guid.NewGuid(), PlayerId, TestKeys.SmallChest);
        progress.UseFree(5, 5, Now.AddMinutes(-1));
        _lootboxes.GetKeyChestProgressAsync(PlayerId, TestKeys.SmallChest, Arg.Any<CancellationToken>()).Returns(progress);

        var refusal = await Assert.ThrowsAsync<InvalidStateException>(() => Open(free: true));

        Assert.Equal(RefusalReasons.KeyChestFreeNotReady.Key, refusal.Reason);
        Assert.Empty(_granted);
    }

    /// <summary>Ключами — по ключу за скриню, кожна скриня — свій кидок.</summary>
    [Fact]
    public async Task Handle_ShouldSpendAKeyPerChest()
    {
        var keys = GivenKeys(3);

        await Open(free: false, count: 2);

        Assert.Equal(1, keys.Count);
        Assert.Equal(2, _granted.Count);
    }

    /// <summary>Останні ключі знімають стек із рюкзака.</summary>
    [Fact]
    public async Task Handle_ShouldRemoveTheStack_WhenTheLastKeysAreSpent()
    {
        var keys = GivenKeys(2);

        await Open(free: false, count: 2);

        _inventory.Received(1).RemoveItem(keys);
    }

    /// <summary>Ключів бракує — відмова з цифрами, ключі не списано.</summary>
    [Fact]
    public async Task Handle_ShouldRefuse_WhenKeysAreShort()
    {
        var keys = GivenKeys(1);

        var refusal = await Assert.ThrowsAsync<RequirementNotMetException>(() => Open(free: false, count: 2));

        Assert.Equal(RefusalReasons.ItemNotEnough.Key, refusal.Reason);
        Assert.Equal(1, keys.Count);
        Assert.Empty(_granted);
    }

    /// <summary>Звичайна скриня — не ключова: 404, а не безкоштовне відкриття чого завгодно.</summary>
    [Fact]
    public async Task Handle_ShouldRefuse_AChestThatIsNotAKeyChest()
        => await Assert.ThrowsAsync<EntityNotFoundException>(() => Open(free: true, chest: TestKeys.BigChest));
}
