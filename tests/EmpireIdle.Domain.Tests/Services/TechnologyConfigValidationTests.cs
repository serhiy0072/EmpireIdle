using EmpireIdle.Domain.Services;
using EmpireIdle.Domain.Services.Config;
using EmpireIdle.TestKit;

namespace EmpireIdle.Domain.Tests.Services;

/// <summary>
/// Технології в конфігу (рішення 28.10.2026). Кожен тест ламає одну річ у валідному конфігу:
/// дерева особистих і кланових технологій, Лабораторія, підношення й клановий магазин.
/// </summary>
public class TechnologyConfigValidationTests
{
    private static GameConfig Valid() => new GameConfigBuilder().WithTechnologies().Build();

    private static InvalidOperationException Rejects(Action<GameConfig> break_)
    {
        var config = Valid();
        break_(config);

        return Assert.Throws<InvalidOperationException>(() => GameConfigValidator.Validate(config));
    }

    private static TechnologyNodeConfig Personal(GameConfig config, string key)
        => config.Technologies.Personal.Nodes.Single(n => n.Key == key);

    [Fact]
    public void Validate_ShouldAcceptTheTechnologyConfig()
        => Assert.Null(Record.Exception(() => GameConfigValidator.Validate(Valid())));

    /// <summary>Без технологій у конфігу Лабораторія не потрібна — секцію пропускаємо.</summary>
    [Fact]
    public void Validate_ShouldSkipEmptyTechnologies()
        => Assert.Null(Record.Exception(() => GameConfigValidator.Validate(new GameConfigBuilder().WithBuildings().Build())));

    [Fact]
    public void Validate_ShouldRejectALaboratoryWithLevels()
        => Assert.Contains("BuildingKey", Rejects(c => c.Buildings.Single(b => b.Key == TestKeys.Laboratory).Upgradable = true).Message);

    [Fact]
    public void Validate_ShouldRejectANodeOnAnUnknownTab()
        => Assert.Contains("unknown tab", Rejects(c => Personal(c, "attack_infantry").Tab = "magic").Message);

    /// <summary>Передумова з того самого ряду дала б цикл або вузол, що відкриває сам себе.</summary>
    [Fact]
    public void Validate_ShouldRejectAPrerequisiteFromTheSameRow()
        => Assert.Contains("earlier row", Rejects(c => Personal(c, "command_infantry").Row = 1).Message);

    [Fact]
    public void Validate_ShouldRejectAnUnknownPrerequisite()
        => Rejects(c => Personal(c, "command_infantry").Requires = ["nothing"]);

    [Fact]
    public void Validate_ShouldRejectANodePastTheFirstRowWithoutPrerequisites()
        => Assert.Contains("without prerequisites", Rejects(c => Personal(c, "command_infantry").Requires = []).Message);

    [Fact]
    public void Validate_ShouldRejectATabWithoutAFirstRow()
        => Assert.Contains("row 1", Rejects(c =>
        {
            Personal(c, "attack_infantry").Row = 2;
            Personal(c, "command_infantry").Row = 3;
        }).Message);

    [Fact]
    public void Validate_ShouldRejectAnAttackAimedAtAResource()
        => Assert.Contains("cannot aim", Rejects(c => Personal(c, "attack_infantry").Effects[0].Target = TestKeys.Gold).Message);

    [Fact]
    public void Validate_ShouldRejectAClanOnlyStatInThePersonalTree()
        => Assert.Contains("clan-only", Rejects(c => Personal(c, "attack_infantry").Effects[0] =
            new TechnologyEffectConfig { Stat = TechnologyStat.CoinBonus, PerLevel = 0.01 }).Message);

    [Fact]
    public void Validate_ShouldRejectTiersWithAGap()
        => Assert.Contains("without gaps", Rejects(c => c.Technologies.Personal.Tiers[1].Tier = 3).Message);

    [Fact]
    public void Validate_ShouldRejectATierThatOpensEarlierThanThePrevious()
        => Assert.Contains("opens earlier", Rejects(c => c.Technologies.Personal.Tiers[1].MainBuildingLevel = 1).Message);

    [Fact]
    public void Validate_ShouldRejectATabWithoutPrices()
        => Assert.Contains("no resource prices", Rejects(c => c.Technologies.Personal.TabResources.Clear()).Message);

    [Fact]
    public void Validate_ShouldRejectAnOfferingResourceWithoutAPrice()
        => Assert.Contains("AttemptCostPerTownHall", Rejects(c =>
            c.Technologies.Clan.Nodes[0].OfferResources = [TestKeys.Iron]).Message);

    [Fact]
    public void Validate_ShouldRejectAnUnknownShopItem()
        => Assert.Contains("unknown item", Rejects(c => c.Technologies.Clan.Shop.Weekly[0].ItemKey = "nothing").Message);

    /// <summary>Річ із ринку в магазині за монети — монети перетекли б у gems через продаж іншим.</summary>
    [Fact]
    public void Validate_ShouldRejectATradeableShopItem()
        => Assert.Contains("tradeable", Rejects(c => c.Items.Single(i => i.Key == TestKeys.ClanShopItem).Tradeable = true).Message);

    [Fact]
    public void Validate_ShouldRejectMoreDailySlotsThanThePoolHolds()
        => Assert.Contains("DailySlots", Rejects(c => c.Technologies.Clan.Shop.DailySlots = 2).Message);
}
