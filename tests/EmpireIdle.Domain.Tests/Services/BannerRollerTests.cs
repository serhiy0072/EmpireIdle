using EmpireIdle.Domain.Enums;
using EmpireIdle.Domain.Services;
using EmpireIdle.Domain.Services.Config;

namespace EmpireIdle.Domain.Tests.Services;

/// <summary>
/// Розіграш банера. Головне тут — що обидві гарантії спрацьовують рівно
/// на своєму кроці, а програний 50/50 робить наступний унікальний банерним.
/// </summary>
public class BannerRollerTests
{
    private const string Featured = "featured_unique";
    private const string OtherUnique = "other_unique";

    private static BannerConfig Banner(bool withAlternativeUnique = true)
    {
        var banner = new BannerConfig
        {
            Key = "test_banner",
            Kind = BannerKind.Hero,
            PityGroup = "hero",
            PriceGems = 150,
            RarePity = 10,
            UniquePity = 50,
            FeaturedKey = Featured,
            Drops =
            [
                Drop(Featured, Rarity.Unique, weight: 1, BannerKind.Hero),
                Drop("rare_hero", Rarity.Rare, weight: 10, BannerKind.Hero),
                Drop("junk", Rarity.Common, weight: 89)
            ]
        };

        if (withAlternativeUnique)
            banner.Drops.Add(Drop(OtherUnique, Rarity.Unique, weight: 1, BannerKind.Hero));

        return banner;
    }

    private static BannerDropConfig Drop(string key, Rarity rarity, int weight, BannerKind? kind = null) => new()
    {
        Key = key,
        DisplayName = key,
        Rarity = rarity,
        Kind = kind,
        Weight = weight,
        Rewards = [new RewardConfig { Type = "Gems", Amount = 1 }]
    };

    private static BannerRoller Roller(BannerConfig banner)
        => new(new ShopConfig { Banners = [banner] });

    private static BannerRollResult Roll(BannerConfig banner, PityState state, int seed = 7)
        => Roller(banner).Roll(banner, state, seed);

    [Fact]
    public void Roll_ShouldBeReproducible_ForTheSameSeed()
    {
        var banner = Banner();

        var first = Roll(banner, PityState.Empty, seed: 1234);
        var second = Roll(banner, PityState.Empty, seed: 1234);

        Assert.Equal(first.Drop.Key, second.Drop.Key);
        Assert.Equal(first.State, second.State);
    }

    [Fact]
    public void FindBanner_ShouldReturnNull_ForAnUnknownKey()
        => Assert.Null(Roller(Banner()).FindBanner("no_such_banner"));

    /// <summary>Крок RarePity видає щонайменше рідкісного, з якого б сіда не крутили.</summary>
    [Theory]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(99)]
    public void Roll_ShouldGuaranteeARare_OnTheRarePityStep(int seed)
    {
        var banner = Banner();

        var result = Roll(banner, new PityState(RareSince: 9, UniqueSince: 0, FeaturedGuaranteed: false), seed);

        Assert.True(result.Drop.Rarity >= Rarity.Rare);
        Assert.True(result.WasPity);
        Assert.Equal(0, result.State.RareSince);
    }

    [Theory]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(99)]
    public void Roll_ShouldGuaranteeAUnique_OnTheUniquePityStep(int seed)
    {
        var banner = Banner();

        var result = Roll(banner, new PityState(RareSince: 3, UniqueSince: 49, FeaturedGuaranteed: false), seed);

        Assert.Equal(Rarity.Unique, result.Drop.Rarity);
        Assert.True(result.WasPity);
        Assert.Equal(0, result.State.UniqueSince);

        // Унікальний закриває й рідкісну гарантію
        Assert.Equal(0, result.State.RareSince);
    }

    [Fact]
    public void Roll_ShouldCountUpBothCounters_OnACommonDrop()
    {
        var banner = Banner();
        var state = new PityState(RareSince: 2, UniqueSince: 8, FeaturedGuaranteed: false);

        // Спільний лот важить 89 зі 101, тож серед сідів знайдеться звичайний
        var result = Enumerable.Range(1, 200)
            .Select(seed => Roll(banner, state, seed))
            .First(r => r.Drop.Rarity == Rarity.Common);

        Assert.Equal(3, result.State.RareSince);
        Assert.Equal(9, result.State.UniqueSince);
        Assert.False(result.WasPity);
    }

    /// <summary>
    /// 50/50: на гарантованому унікальному трапляються обидва результати,
    /// і програш завжди піднімає прапорець на наступний раз.
    /// </summary>
    [Fact]
    public void Roll_ShouldSplitUniquesBetweenTheFeaturedAndTheRest()
    {
        var banner = Banner();
        var state = new PityState(RareSince: 0, UniqueSince: 49, FeaturedGuaranteed: false);

        var results = Enumerable.Range(1, 200).Select(seed => Roll(banner, state, seed)).ToList();

        Assert.Contains(results, r => r.Drop.Key == Featured);
        Assert.Contains(results, r => r.Drop.Key == OtherUnique);

        Assert.All(results, r =>
        {
            Assert.Equal(r.Drop.Key != Featured, r.LostFiftyFifty);
            Assert.Equal(r.Drop.Key != Featured, r.State.FeaturedGuaranteed);
        });
    }

    [Theory]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(99)]
    public void Roll_ShouldGiveTheFeatured_WhenTheFiftyFiftyWasLostBefore(int seed)
    {
        var banner = Banner();

        var result = Roll(banner, new PityState(RareSince: 0, UniqueSince: 49, FeaturedGuaranteed: true), seed);

        Assert.Equal(Featured, result.Drop.Key);
        Assert.False(result.LostFiftyFifty);
        Assert.False(result.State.FeaturedGuaranteed);
    }

    /// <summary>Єдиний унікальний у пулі означає, що програвати нічого.</summary>
    [Theory]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(99)]
    public void Roll_ShouldNeverLoseTheFiftyFifty_WhenTheFeaturedIsTheOnlyUnique(int seed)
    {
        var banner = Banner(withAlternativeUnique: false);

        var result = Roll(banner, new PityState(RareSince: 0, UniqueSince: 49, FeaturedGuaranteed: false), seed);

        Assert.Equal(Featured, result.Drop.Key);
        Assert.False(result.LostFiftyFifty);
    }

    [Fact]
    public void Odds_ShouldSumToHundred()
    {
        var banner = Banner();

        Assert.Equal(100.0, Roller(banner).Odds(banner).Values.Sum(), 1);
    }

    /// <summary>Філер чужої категорії не закриває гарантію й не скидає лічильники.</summary>
    [Fact]
    public void Roll_ShouldNotResetCounters_OnAFillerDrop()
    {
        var banner = Banner();
        var state = new PityState(RareSince: 2, UniqueSince: 8, FeaturedGuaranteed: false);

        var result = Enumerable.Range(1, 200)
            .Select(seed => Roll(banner, state, seed))
            .First(r => r.Drop.Kind is null);

        Assert.Equal(3, result.State.RareSince);
        Assert.Equal(9, result.State.UniqueSince);
    }

    /// <summary>Гарантія героїчного банера видає лише героїв.</summary>
    [Theory]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(99)]
    public void Roll_ShouldPayThePityInTheBannerKind(int seed)
    {
        var banner = Banner();
        banner.Drops.Add(Drop("filler_weapon", Rarity.Common, weight: 50, BannerKind.Weapon));

        var result = Roll(banner, new PityState(RareSince: 9, UniqueSince: 0, FeaturedGuaranteed: false), seed);

        Assert.Equal(BannerKind.Hero, result.Drop.Kind);
    }
}
