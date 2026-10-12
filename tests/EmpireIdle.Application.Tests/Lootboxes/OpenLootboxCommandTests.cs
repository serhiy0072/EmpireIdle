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
/// Відкриття скрині з рюкзака (GDD §9.12): списання, гарантія, вкладені скрині й випадкові
/// артефакти. Таблиці в тестах звужені до однієї позиції — щоб результат не залежав від сіда.
/// </summary>
public class OpenLootboxCommandTests
{
    private static readonly DateTime Now = new(2026, 10, 11, 12, 0, 0, DateTimeKind.Utc);
    private static readonly Guid PlayerId = Guid.NewGuid();

    private readonly IInventoryRepository _inventory = Substitute.For<IInventoryRepository>();
    private readonly ILootboxRepository _lootboxes = Substitute.For<ILootboxRepository>();
    private readonly IVillageRepository _villages = Substitute.For<IVillageRepository>();
    private readonly IUnitOfWork _unitOfWork = Substitute.For<IUnitOfWork>();
    private readonly IRandomSource _random = Substitute.For<IRandomSource>();
    private readonly List<RewardConfig> _granted = [];
    private readonly List<LootboxPity> _added = [];
    private readonly GameConfig _config = new GameConfigBuilder().WithLootboxes().Build();

    public OpenLootboxCommandTests()
    {
        _villages.GetByPlayerIdReadOnlyAsync(PlayerId, Arg.Any<CancellationToken>()).Returns(Entities.VillageWithTownhall());
        _lootboxes.AddPityAsync(Arg.Do<LootboxPity>(_added.Add), Arg.Any<CancellationToken>());
    }

    private LootboxConfig Box(string key) => _config.Lootboxes.Single(b => b.Key == key);

    /// <summary>Лишає в таблиці одну позицію — ту, що задовольняє умову.</summary>
    private void Only(string key, Predicate<LootboxEntryConfig> keep) => Box(key).Entries.RemoveAll(e => !keep(e));

    private PlayerItem GivenChests(string key, int count)
    {
        var stack = new PlayerItem(Guid.NewGuid(), PlayerId, key, count);
        _inventory.GetItemAsync(PlayerId, key, Arg.Any<CancellationToken>()).Returns(stack);
        return stack;
    }

    private IRewardGranter Granter(string type)
    {
        var granter = Substitute.For<IRewardGranter>();
        granter.RewardType.Returns(type);
        granter.GrantAsync(Arg.Do<RewardContext>(c => _granted.Add(c.Reward)), Arg.Any<CancellationToken>());
        return granter;
    }

    private Task<LootboxOpening> Open(string key, int count = 1)
    {
        var catalog = new GameCatalog(_config);
        var dispatcher = new RewardDispatcher([Granter("Resource"), Granter("Gems"), Granter("Equipment"), Granter("Item")]);
        var opener = new LootboxOpener(_lootboxes, _villages, dispatcher, catalog, _random);

        return new OpenLootboxCommandHandler(_inventory, opener, catalog, _unitOfWork, new FakeTimeProvider(Now),
                NullLogger<OpenLootboxCommandHandler>.Instance)
            .Handle(new OpenLootboxCommand(PlayerId, key, count), CancellationToken.None);
    }

    /// <summary>Скриня з кількома кидками дає стільки нагород за одне відкриття (ключова скриня — 4).</summary>
    [Fact]
    public async Task Handle_ShouldRollTheTableSeveralTimes_ForAMultiRollChest()
    {
        Only(TestKeys.SmallChest, e => e.Type == "Resource");
        Box(TestKeys.SmallChest).Rolls = 4;
        GivenChests(TestKeys.SmallChest, 2);

        var opening = await Open(TestKeys.SmallChest, count: 2);

        Assert.Equal(8, _granted.Count);
        Assert.Equal(8, opening.Drops.Count);
    }

    /// <summary>Дві скрині — два кидки й дві видачі; останні скрині знімають стек; одне збереження.</summary>
    [Fact]
    public async Task Handle_ShouldOpenEachChest_AndSpendTheStack()
    {
        Only(TestKeys.SmallChest, e => e.Type == "Resource");
        var stack = GivenChests(TestKeys.SmallChest, 2);

        var opening = await Open(TestKeys.SmallChest, count: 2);

        Assert.Equal(2, opening.Drops.Count);
        Assert.All(opening.Drops, d => Assert.Equal(new LootboxDrop("Resource", TestKeys.Gold, 500, TestKeys.SmallChest, false), d));
        Assert.Equal(2, _granted.Count);
        Assert.Equal(0, stack.Count);
        _inventory.Received(1).RemoveItem(stack);
        await _unitOfWork.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_ShouldRefuse_WhenTheBackpackHoldsFewerChests()
    {
        var stack = GivenChests(TestKeys.SmallChest, 1);

        var refusal = await Assert.ThrowsAsync<RequirementNotMetException>(() => Open(TestKeys.SmallChest, count: 2));

        Assert.Equal(RefusalReasons.ItemNotEnough.Key, refusal.Reason);
        Assert.Equal(1, stack.Count);
        Assert.Empty(_granted);
    }

    /// <summary>Звичайний предмет — не скриня, хоч і лежить у рюкзаку.</summary>
    [Fact]
    public async Task Handle_ShouldNotOpenAnItemThatIsNotALootbox()
        => await Assert.ThrowsAsync<EntityNotFoundException>(() => Open($"{TestKeys.DungeonSetKey}_common_ring"));

    /// <summary>Вкладена скриня розкривається одразу: рядки — з неї, лічильник — свій у кожної.</summary>
    [Fact]
    public async Task Handle_ShouldOpenNestedChestsRightAway()
    {
        Box(TestKeys.BigChest).PityWithin = 0;
        Box(TestKeys.BigChest).Entries[2].Pity = false;
        Only(TestKeys.BigChest, e => e.IsLootbox);
        Only(TestKeys.SmallChest, e => e.Type == "Gems");
        GivenChests(TestKeys.BigChest, 1);

        var opening = await Open(TestKeys.BigChest);

        Assert.Equal(2, opening.Drops.Count);
        Assert.All(opening.Drops, d => Assert.Equal(new LootboxDrop("Gems", null, 10, TestKeys.SmallChest, false), d));
        Assert.Equal([TestKeys.BigChest, TestKeys.SmallChest], _added.Select(p => p.LootboxKey).Order());
        Assert.Equal(2, _added.Single(p => p.LootboxKey == TestKeys.SmallChest).TotalOpened);
    }

    /// <summary>
    /// Третє відкриття поспіль без артефакта — гарантія: унікальний артефакт із набору данжу,
    /// уже конкретним предметом, і лічильник із нуля.
    /// </summary>
    [Fact]
    public async Task Handle_ShouldPayThePity_WithAConcreteArtifact()
    {
        var pity = new LootboxPity(Guid.NewGuid(), PlayerId, TestKeys.BigChest);
        pity.Apply(1);
        pity.Apply(2);
        _lootboxes.GetPityAsync(PlayerId, TestKeys.BigChest, Arg.Any<CancellationToken>()).Returns(pity);
        _random.Next(Arg.Any<int>()).Returns(0);
        GivenChests(TestKeys.BigChest, 1);

        var opening = await Open(TestKeys.BigChest);

        var drop = Assert.Single(opening.Drops);
        Assert.Equal(("Equipment", 1, true), (drop.Type, drop.Amount, drop.WasPity));
        Assert.Equal($"{TestKeys.DungeonSetKey}_unique", new GameCatalog(_config).Item(drop.Key!).SetKey);
        Assert.Equal(0, pity.Since);
        Assert.Empty(_added);
    }

    /// <summary>Жоден данж ще не відкритий ратушею — артефакт із найпершого: скриня не видає порожнечу.</summary>
    [Fact]
    public async Task Handle_ShouldFallBackToTheFirstDungeon_ForANewcomer()
    {
        _config.Dungeons.Dungeons[0].RequiresMainBuildingLevel = 5;
        _villages.GetByPlayerIdReadOnlyAsync(PlayerId, Arg.Any<CancellationToken>()).Returns(Entities.VillageWithTownhall(townhallLevel: 1));
        Box(TestKeys.BigChest).PityWithin = 0;
        Only(TestKeys.BigChest, e => e.IsArtifact);
        Box(TestKeys.BigChest).Entries[0].Pity = false;
        GivenChests(TestKeys.BigChest, 1);

        var opening = await Open(TestKeys.BigChest);

        Assert.StartsWith($"{TestKeys.DungeonSetKey}_unique", Assert.Single(opening.Drops).Key);
    }
}
