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
        config.Equipment = GameConfigBuilder.DefaultEquipment(TestKeys.Forge);
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
    /// Стати — база з рівнем і сталим бонусом плюс відсотки випадкових; без множника класу,
    /// бо предмет ще не на герої. Поруч — скільки бракує до рівня й скільки предмет дасть,
    /// якщо його згодувати.
    /// </summary>
    [Fact]
    public async Task Handle_ShouldReportTheItemsStats_AndFeedingNumbers()
    {
        var amulet = Entities.Equipment("amulet", EquipmentSlot.Artifact, PlayerId, bonuses: [(0, "Attack", 10), (2, "CritChance", 2)]);
        // Кроки до 1-го й 2-го рівня — по 100: 200 досвіду дають рівень 2 рівно
        amulet.GainExperience(200, 2, Now);

        _inventory.GetEquipmentAsync(PlayerId, Arg.Any<CancellationToken>()).Returns([amulet]);

        var view = await Handler().Handle(new GetInventoryQuery(PlayerId), CancellationToken.None);

        var shown = Assert.Single(view.Equipment);
        // (10 + 2 × 2) × 1.1; захист без бонусу; юнітів — 40 + 2 × 8
        Assert.Equal(15.4, shown.Stats["Attack"], precision: 6);
        Assert.Equal(14.0, shown.Stats["Defense"], precision: 6);
        Assert.Equal(56.0, shown.Stats["UnitAttack"], precision: 6);
        Assert.Equal(2.0, shown.Stats["CritChance"], precision: 6);
        Assert.Equal((2, 200L, 2), (shown.Level, shown.Experience, shown.Mastery));
        // До 3-го рівня — крок 110
        Assert.Equal(110L, shown.ExperienceToNext);
        // Звичайний: 100 за рідкість плюс увесь вкладений
        Assert.Equal(300L, shown.FeedValue);
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
