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
/// Інвентар для показу збирає запит, а не контролер: описи з каталогу, стати з рівнем і майстерністю
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
        config.Equipment.LevelBonusPerLevel = 0.05;
        config.Equipment.MasteryBonusPerLevel = 0.1;
        config.Equipment.FeedExperience = new Dictionary<Rarity, int> { [Rarity.Common] = 100 };
        config.Items.Add(new ItemConfig
        {
            Key = "wood_crate", DisplayName = "Ящик дерева", Description = "+500 дерева", Rarity = Rarity.Rare, Type = "resource"
        });

        return new GetInventoryQueryHandler(_inventory, _effects, new GameCatalog(config), new FakeTimeProvider(Now),
            new ArtifactProgression(config.Equipment));
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

    /// <summary>
    /// Стати — ті самі, з якими предмет піде в бій: рівень і майстерність із конфіга.
    /// Поруч — скільки бракує до рівня й скільки предмет дасть, якщо його згодувати.
    /// </summary>
    [Fact]
    public async Task Handle_ShouldReportLevelledStats_AndFeedingNumbers()
    {
        var amulet = Entities.Equipment("amulet", EquipmentSlot.Artifact, PlayerId, stats: ("Attack", 100.0));
        // Крок до 1-го рівня — 40, до 2-го — 110: 150 досвіду дають рівень 2 рівно
        amulet.GainExperience(150, 2, Now);
        amulet.RaiseMastery(Now);

        _inventory.GetEquipmentAsync(PlayerId, Arg.Any<CancellationToken>()).Returns([amulet]);

        var view = await Handler().Handle(new GetInventoryQuery(PlayerId), CancellationToken.None);

        var shown = Assert.Single(view.Equipment);
        // 100 × (1 + 2·0.05 + 1·0.1)
        Assert.Equal(120.0, shown.Stats["Attack"], precision: 6);
        Assert.Equal((2, 150L, 1), (shown.Level, shown.Experience, shown.Mastery));
        // До 3-го рівня — крок 190
        Assert.Equal(190L, shown.ExperienceToNext);
        // Звичайний: 100 за рідкість плюс увесь вкладений
        Assert.Equal(250L, shown.FeedValue);
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
