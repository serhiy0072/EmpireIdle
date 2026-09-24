using EmpireIdle.Domain.Enums;
using EmpireIdle.Domain.Services;
using EmpireIdle.Domain.Services.Config;

namespace EmpireIdle.Domain.Tests.Services;

/// <summary>
/// Ціни ринку: якір, обрізана медіана й коридор. Коридор, який можна
/// зсунути вошем між двома акаунтами, гірший за відсутність коридору.
/// </summary>
public class MarketPricingTests
{
    private static MarketPricing Pricing(Action<MarketConfig>? tune = null)
    {
        var config = new GameConfig
        {
            Shop = new ShopConfig { Items = [new ShopItemConfig { ItemKey = "boost", PriceGems = 10 }] },
            Market = new MarketConfig
            {
                GoldPerGem = 100,
                GoldPerPower = new Dictionary<string, double> { ["weapon"] = 10, ["hero.Rare"] = 30 },
                CorridorShare = 0.3,
                MinSalesForMedian = 5,
                OutlierTrimShare = 0.1,
                MedianMinAnchorShare = 0.5,
                MedianMaxAnchorShare = 2.0,
                ListingTaxShare = 0.05,
                BaseListings = 1,
                ListingsPerBuildingLevel = 1
            }
        };

        tune?.Invoke(config.Market);

        return new MarketPricing(config);
    }

    [Fact]
    public void CategoryOf_ShouldSeparateWeaponsArtifactsHeroRanksAndItems()
    {
        Assert.Equal("weapon", MarketPricing.CategoryOf(EquipmentSlot.Weapon));
        Assert.Equal("artifact", MarketPricing.CategoryOf(EquipmentSlot.Artifact));
        Assert.Equal("hero.Unique", MarketPricing.CategoryOf(Rarity.Unique));
        Assert.Equal("item.boost", MarketPricing.CategoryOfItem("boost"));
    }

    /// <summary>Стаковий предмет прив'язаний до своєї ціни в крамниці: 10 gems × 100.</summary>
    [Fact]
    public void AnchorPerUnit_ShouldConvertTheShopPriceInGems()
        => Assert.Equal(1000, Pricing().AnchorPerUnit("item.boost"));

    [Fact]
    public void AnchorPerUnit_ShouldTakeThePowerPriceOfTheCategory()
        => Assert.Equal(30, Pricing().AnchorPerUnit("hero.Rare"));

    /// <summary>Без якоря торгувати не можна: коридор із першого продажу вирішував би вош.</summary>
    [Fact]
    public void Corridor_ShouldBeAbsent_WithoutAnAnchor()
        => Assert.Null(Pricing().Corridor("item.unknown", median: null));

    [Fact]
    public void Median_ShouldBeAbsent_WhileThereAreTooFewSales()
        => Assert.Null(Pricing().Median([10, 11, 12, 13]));

    /// <summary>По одному викиду з кожного боку відкидається (10% від десяти), лишається середина.</summary>
    [Fact]
    public void Median_ShouldTrimOutliers()
    {
        var median = Pricing().Median([1, 10, 10, 11, 11, 12, 12, 13, 13, 5000]);

        Assert.Equal(11.5, median);
    }

    [Fact]
    public void Corridor_ShouldSpreadThirtyPercentAroundTheAnchor_WithoutAMedian()
    {
        var corridor = Pricing().Corridor("weapon", median: null)!.Value;

        Assert.Equal(7, corridor.MinPerUnit, 3);
        Assert.Equal(13, corridor.MaxPerUnit, 3);
    }

    [Fact]
    public void Corridor_ShouldFollowTheMedian_WithinTheAnchorBand()
    {
        var corridor = Pricing().Corridor("weapon", median: 15)!.Value;

        Assert.Equal(10.5, corridor.MinPerUnit, 3);
        Assert.Equal(19.5, corridor.MaxPerUnit, 3);
    }

    /// <summary>Медіана, розігнана вошем у 100 разів, притискається до двох якорів.</summary>
    [Fact]
    public void Corridor_ShouldClampAWashTradedMedianToTheAnchorBand()
    {
        var corridor = Pricing().Corridor("weapon", median: 1000)!.Value;

        Assert.Equal(14, corridor.MinPerUnit, 3);
        Assert.Equal(26, corridor.MaxPerUnit, 3);
    }

    /// <summary>Межі цілі й самі проходять перевірку: мінімум угору, максимум униз.</summary>
    [Fact]
    public void PriceCorridor_ShouldRoundItsBoundsInwards()
    {
        var corridor = new PriceCorridor(7.3, 12.7);

        Assert.Equal(22, corridor.MinPrice(3));
        Assert.Equal(38, corridor.MaxPrice(3));
        Assert.True(corridor.Allows(22, 3));
        Assert.True(corridor.Allows(38, 3));
        Assert.False(corridor.Allows(21, 3));
        Assert.False(corridor.Allows(39, 3));
    }

    [Fact]
    public void ListingTax_ShouldBeAShareOfThePrice_ButAtLeastOneGold()
    {
        Assert.Equal(50, Pricing().ListingTax(1000));
        Assert.Equal(1, Pricing().ListingTax(3));
    }

    [Fact]
    public void ListingLimit_ShouldGrowWithTheMarketLevel()
    {
        Assert.Equal(2, Pricing().ListingLimit(1));
        Assert.Equal(6, Pricing().ListingLimit(5));
    }
}
