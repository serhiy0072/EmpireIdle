using EmpireIdle.Application.Interfaces;
using EmpireIdle.Application.Inventory.Queries;
using EmpireIdle.Domain.Entities;
using EmpireIdle.Domain.Enums;
using EmpireIdle.Domain.Services;
using EmpireIdle.Domain.Services.Config;
using EmpireIdle.TestKit;
using Microsoft.Extensions.Time.Testing;
using NSubstitute;

namespace EmpireIdle.Application.Tests.Inventory;

/// <summary>
/// Інвентар для показу збирає запит, а не контролер: описи з каталогу, стати із заточкою
/// за кривою з конфіга й лише діючі бусти.
/// </summary>
public class GetInventoryQueryTests
{
    private static readonly DateTime Now = Entities.Now;
    private static readonly Guid PlayerId = Guid.NewGuid();

    private readonly IInventoryRepository _inventory = Substitute.For<IInventoryRepository>();
    private readonly IActiveEffectRepository _effects = Substitute.For<IActiveEffectRepository>();

    public GetInventoryQueryTests()
    {
        _inventory.GetItemsAsync(PlayerId, Arg.Any<CancellationToken>()).Returns([]);
        _inventory.GetEquipmentAsync(PlayerId, Arg.Any<CancellationToken>()).Returns([]);
        _effects.GetByPlayerAsync(PlayerId, Arg.Any<CancellationToken>()).Returns([]);
    }

    private GetInventoryQueryHandler Handler()
    {
        var config = new GameConfigBuilder().WithBuildings().Build();
        config.Equipment.EnhancementBonusPerLevel = 0.1;
        config.Items.Add(new ItemConfig
        {
            Key = "wood_crate", DisplayName = "Ящик дерева", Description = "+500 дерева", Rarity = Rarity.Rare, Type = "resource"
        });

        return new GetInventoryQueryHandler(_inventory, _effects, new GameCatalog(config), new FakeTimeProvider(Now));
    }

    [Fact]
    public async Task Handle_ShouldDescribeItemsFromTheCatalog_AndKeepUnknownKeysAsIs()
    {
        _inventory.GetItemsAsync(PlayerId, Arg.Any<CancellationToken>()).Returns(
        [
            new PlayerItem(Guid.NewGuid(), PlayerId, "wood_crate", 3),
            new PlayerItem(Guid.NewGuid(), PlayerId, "retired_item", 1)
        ]);

        var view = await Handler().Handle(new GetInventoryQuery(PlayerId), CancellationToken.None);

        var crate = Assert.Single(view.Items, i => i.ItemKey == "wood_crate");
        Assert.Equal(("Ящик дерева", "+500 дерева", Rarity.Rare, "resource", 3),
            (crate.DisplayName, crate.Description, crate.Rarity, crate.Type, crate.Count));

        var retired = Assert.Single(view.Items, i => i.ItemKey == "retired_item");
        Assert.Equal(("retired_item", Rarity.Common, "unknown"), (retired.DisplayName, retired.Rarity, retired.Type));
    }

    /// <summary>Стати — ті самі, з якими предмет піде в бій: з заточкою, а зламаний — нулі.</summary>
    [Fact]
    public async Task Handle_ShouldReportEnhancedStats_AndZeroForABrokenItem()
    {
        var sword = Entities.Equipment("iron_sword", EquipmentSlot.Artifact, PlayerId, stats: ("Attack", 100.0));
        sword.Enhance(Now);
        sword.Enhance(Now);

        var broken = Entities.Equipment("old_sword", EquipmentSlot.Artifact, PlayerId, stats: ("Attack", 100.0));
        broken.Break(Now);

        _inventory.GetEquipmentAsync(PlayerId, Arg.Any<CancellationToken>()).Returns([sword, broken]);

        var view = await Handler().Handle(new GetInventoryQuery(PlayerId), CancellationToken.None);

        // Дві заточки по 10% із конфіга
        Assert.Equal(120.0, view.Equipment.Single(e => e.Id == sword.Id).Stats["Attack"], precision: 6);
        Assert.Equal(0.0, view.Equipment.Single(e => e.Id == broken.Id).Stats["Attack"]);
    }

    [Fact]
    public async Task Handle_ShouldOnlyListEffectsThatAreStillActive()
    {
        _effects.GetByPlayerAsync(PlayerId, Arg.Any<CancellationToken>()).Returns(
        [
            new ActiveEffect(Guid.NewGuid(), PlayerId, EffectTarget.Production, 1.5, Now.AddHours(-2), Now.AddHours(1), "boost_live"),
            new ActiveEffect(Guid.NewGuid(), PlayerId, EffectTarget.Production, 1.5, Now.AddHours(-2), Now.AddMinutes(-1), "boost_gone")
        ]);

        var view = await Handler().Handle(new GetInventoryQuery(PlayerId), CancellationToken.None);

        Assert.Equal("boost_live", Assert.Single(view.ActiveEffects).SourceItemKey);
    }
}
