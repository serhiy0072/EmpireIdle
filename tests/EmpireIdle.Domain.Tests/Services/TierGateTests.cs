using EmpireIdle.Domain.Enums;
using EmpireIdle.Domain.Services;
using EmpireIdle.Domain.Services.Config;
using EmpireIdle.TestKit;

namespace EmpireIdle.Domain.Tests.Services
{
    /// <summary>
    /// Тір героя відкривається з рівнем світу (GDD §6.1): банер не видає героя вищого тіру, ніж світ,
    /// з якого він відкритий, а предмет апу продається лише вікном після відкриття свого тіру.
    /// </summary>
    public class TierGateTests
    {
        private static readonly DateTime LevelSince = new(2026, 10, 1, 0, 0, 0, DateTimeKind.Utc);

        private static GameConfig Config(int heroTier = 1, int bannerLevel = 1)
        {
            var config = new GameConfigBuilder().WithHeroes().Build();
            config.Heroes.Single(h => h.Key == TestKeys.UniqueHero).NativeTier = heroTier;

            // Банер несе лише рідкісних і унікальних героїв — рідкісний тут копія унікального
            var unique = config.Heroes.Single(h => h.Key == TestKeys.UniqueHero);
            config.Heroes.Add(new HeroConfig
            {
                Key = "rare_tier_hero", DisplayName = "Rare", Class = unique.Class, Rank = Rarity.Rare,
                BaseStats = unique.BaseStats, StatGrowth = unique.StatGrowth
            });
            config.Shop.Banners =
            [
                new BannerConfig
                {
                    Key = "tier_banner",
                    DisplayName = "Tier banner",
                    Kind = BannerKind.Hero,
                    PityGroup = "hero",
                    PriceGems = 160,
                    RarePity = 10,
                    UniquePity = 50,
                    RequiresServerLevel = bannerLevel,
                    Drops =
                    [
                        new BannerDropConfig
                        {
                            Key = "tier_rare", DisplayName = "Rare", Rarity = Rarity.Rare, Kind = BannerKind.Hero, Weight = 1,
                            Rewards = [new RewardConfig { Type = "Hero", Key = "rare_tier_hero", Amount = 1 }]
                        },
                        new BannerDropConfig
                        {
                            Key = "tier_hero", DisplayName = "Hero", Rarity = Rarity.Unique, Kind = BannerKind.Hero, Weight = 1,
                            Rewards = [new RewardConfig { Type = "Hero", Key = TestKeys.UniqueHero, Amount = 1 }]
                        }
                    ]
                }
            ];

            return config;
        }

        // ---------- Банери ----------

        [Fact]
        public void Validate_ShouldAccept_AHeroOfTheBannersWorldLevel()
            => GameConfigValidator.Validate(Config(heroTier: 2, bannerLevel: 2));

        /// <summary>Герой T2 у банері, відкритому зі світу 1, прийшов би раніше за свій тір.</summary>
        [Fact]
        public void Validate_ShouldReject_AHeroAboveTheBannersWorldLevel()
        {
            var error = Assert.Throws<InvalidOperationException>(() => GameConfigValidator.Validate(Config(heroTier: 2, bannerLevel: 1)));

            Assert.Contains("tier_banner", error.Message);
        }

        /// <summary>Банер видає осколки героя (GDD §6.1): лот з осколками — повноцінний «героїчний» лот.</summary>
        [Fact]
        public void Validate_ShouldAccept_HeroShardsAsABannerHeroDrop()
        {
            var config = Config();
            ToShards(config);

            GameConfigValidator.Validate(config);
        }

        /// <summary>Осколки героя T2 з банера світу 1 відкрили б героя раніше за його тір.</summary>
        [Fact]
        public void Validate_ShouldReject_ShardsOfAHeroAboveTheBannersWorldLevel()
        {
            var config = Config(heroTier: 2, bannerLevel: 1);
            ToShards(config);

            Assert.Throws<InvalidOperationException>(() => GameConfigValidator.Validate(config));
        }

        private static void ToShards(GameConfig config)
        {
            foreach (var reward in config.Shop.Banners.SelectMany(b => b.Drops).SelectMany(d => d.Rewards).Where(r => r.Type == "Hero"))
            {
                reward.Type = "HeroShards";
                reward.Amount = 10;
            }
        }

        // ---------- Вікно продажу ----------

        /// <summary>Вікно довше за рівень світу пережило б свій тір — модель вікна його ніколи б не закрила.</summary>
        [Fact]
        public void Validate_ShouldReject_AWindowLongerThanAWorldLevel()
        {
            var config = Config();
            config.Items.Add(new ItemConfig { Key = "essence", DisplayName = "Essence", Type = "evolution" });
            config.Shop.Items.Add(new ShopItemConfig
            {
                ItemKey = "essence", PriceGems = 10, ServerLevel = 2, WindowDays = config.Map.Evolution.DaysPerLevel + 1
            });

            var error = Assert.Throws<InvalidOperationException>(() => GameConfigValidator.Validate(config));

            Assert.Contains("essence", error.Message);
        }

        [Fact]
        public void IsOnSaleAt_ShouldOpen_ForTheWindowDaysOfItsWorldLevel()
        {
            var offer = new ShopItemConfig { ItemKey = "essence", ServerLevel = 2, WindowDays = 14 };

            Assert.True(offer.IsOnSaleAt(serverLevel: 2, LevelSince, LevelSince.AddDays(13)));
            Assert.False(offer.IsOnSaleAt(serverLevel: 2, LevelSince, LevelSince.AddDays(14)));
            Assert.Equal(LevelSince.AddDays(14), offer.OnSaleUntil(serverLevel: 2, LevelSince));
        }

        /// <summary>Вікно належить своєму рівню: ні до нього, ні після нього пропозиції немає.</summary>
        [Fact]
        public void IsOnSaleAt_ShouldStayClosed_OnOtherWorldLevels()
        {
            var offer = new ShopItemConfig { ItemKey = "essence", ServerLevel = 2, WindowDays = 14 };

            Assert.False(offer.IsOnSaleAt(serverLevel: 1, LevelSince, LevelSince.AddDays(1)));
            Assert.False(offer.IsOnSaleAt(serverLevel: 3, LevelSince, LevelSince.AddDays(1)));
        }

        [Fact]
        public void IsOnSaleAt_ShouldAlwaysBeOpen_WithoutAWindow()
        {
            var offer = new ShopItemConfig { ItemKey = "crate" };

            Assert.True(offer.IsOnSaleAt(serverLevel: 7, LevelSince, LevelSince.AddYears(1)));
            Assert.Null(offer.OnSaleUntil(serverLevel: 7, LevelSince));
        }
    }
}
