using EmpireIdle.Domain.Services;
using EmpireIdle.Domain.Services.Config;
using EmpireIdle.TestKit;

namespace EmpireIdle.Domain.Tests.Services;

/// <summary>
/// Ринок у конфігу: вимкнений не перевіряється, увімкнений вимагає будівлю
/// й якір ціни для кожного товару. Товар без якоря торгувався б у коридорі,
/// який задає перший же вош.
/// </summary>
public class MarketConfigValidationTests
{
    private const string Market = "market";
    private const string Tonic = "tonic";

    /// <summary>
    /// Герої й спорядження з будівлею ринку, якорями сили для всіх категорій
    /// і одним стаковим товаром, що продається в крамниці.
    /// </summary>
    private static GameConfig Enabled()
    {
        // Будівлі першими: WithBuildings задає список, а решта фікстур лише доповнює його
        var config = new GameConfigBuilder().WithBuildings(Market).WithHeroes().WithEquipment().Build();

        config.Items.Add(new ItemConfig { Key = Tonic, Type = "boost", DisplayName = "Tonic", Description = "t", Tradeable = true });
        config.Shop = new ShopConfig { Items = [new ShopItemConfig { ItemKey = Tonic, PriceGems = 5 }] };
        config.Market = new MarketConfig
        {
            BuildingKey = Market,
            GoldPerPower = new Dictionary<string, double> { ["artifact"] = 10.0 }
        };

        return config;
    }

    private static InvalidOperationException Rejects(Action<GameConfig> break_)
    {
        var config = Enabled();
        break_(config);

        return Assert.Throws<InvalidOperationException>(() => GameConfigValidator.Validate(config));
    }

    [Fact]
    public void Validate_ShouldAcceptAnEnabledMarket()
        => Assert.Null(Record.Exception(() => GameConfigValidator.Validate(Enabled())));

    /// <summary>Без BuildingKey ринку немає — і будівлі чи якорів від конфіга не вимагаємо.</summary>
    [Fact]
    public void Validate_ShouldSkipADisabledMarket()
    {
        var config = new GameConfigBuilder().WithHeroes().WithEquipment().Build();

        Assert.Null(Record.Exception(() => GameConfigValidator.Validate(config)));
    }

    [Fact]
    public void Validate_ShouldRejectAnUnknownMarketBuilding()
        => Assert.Contains("nowhere", Rejects(c => c.Market.BuildingKey = "nowhere").Message);

    /// <summary>Стаковий товар поза крамницею не має якоря — так і з безкоштовними нагородами.</summary>
    [Fact]
    public void Validate_ShouldRejectATradeableItemWithoutAShopPrice()
        => Assert.Contains($"item.{Tonic}", Rejects(c => c.Shop.Items.Clear()).Message);

    /// <summary>Предмет лише зі скрині тримає якір довідковою ціною (GDD §9.12).</summary>
    [Fact]
    public void Validate_ShouldAcceptAChestOnlyItem_WithAGemValue()
    {
        var config = Enabled();
        config.Shop.Items.Clear();
        config.Market.GemValues[Tonic] = 5;

        Assert.Null(Record.Exception(() => GameConfigValidator.Validate(config)));
    }

    [Fact]
    public void Validate_ShouldRejectAGemValueOfAnUnknownItem()
        => Assert.Contains("elixir", Rejects(c => c.Market.GemValues["elixir"] = 5).Message);

    [Fact]
    public void Validate_ShouldRejectANonPositiveGemValue()
        => Assert.Contains(Tonic, Rejects(c => c.Market.GemValues[Tonic] = 0).Message);

    [Fact]
    public void Validate_ShouldRejectAnEquipmentCategoryWithoutAPowerPrice()
        => Assert.Contains("artifact", Rejects(c => c.Market.GoldPerPower.Remove("artifact")).Message);

    /// <summary>Спорядження торгується завжди; позначка на ньому — плутанина в конфігу.</summary>
    [Fact]
    public void Validate_ShouldRejectTheTradeableFlagOnEquipment()
        => Assert.Contains(TestKeys.Artifact,
            Rejects(c => c.Items.Single(i => i.Key == TestKeys.Artifact).Tradeable = true).Message);
}
