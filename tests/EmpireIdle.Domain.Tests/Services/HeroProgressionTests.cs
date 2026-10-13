using EmpireIdle.Domain.Services;
using EmpireIdle.Domain.Services.Config;

namespace EmpireIdle.Domain.Tests.Services
{
    /// <summary>
    /// Криві героя. Дві межі рівня незалежні, і саме їхня взаємодія —
    /// те, що визначає, коли еволюція взагалі щось дає.
    /// </summary>
    public class HeroProgressionTests
    {
        private static HeroProgression Create(
            int levelsPerTier = 10,
            int maxMarches = 8,
            double tierGrowth = 1.10,
            double evolutionPenalty = 0.95,
            List<string>? evolutionItems = null)
            => new(new HeroesConfig
            {
                MaxMarches = maxMarches,
                TierGrowth = tierGrowth,
                EvolutionPenalty = evolutionPenalty,
                EvolutionItemKeys = evolutionItems ?? ["hero_essence_t2", "hero_essence_t3"],
                HealCostPerLevel = [new ResourceCost { Resource = "food", Amount = 40 }],
});

        private static HeroProgression Progression(HeroesConfig? settings = null)
            => new(settings ?? Settings());

        private static HeroesConfig Settings() => new()
        {
            TierGrowth = 1.10,
            EvolutionPenalty = 0.95,
            EvolutionItemKeys = ["hero_essence_t2", "hero_essence_t3"],
            StarPartCosts = [[1, 1, 2, 2, 2, 2], [5, 5, 5, 5, 5, 5], [10, 10, 10, 10, 10, 10], [20, 20, 20, 20, 20, 20], [40, 40, 40, 40, 40, 40]],
            MaxMarches = 8,
            HealCostPerLevel = [new ResourceCost { Resource = "food", Amount = 40 }],
};

        private static HeroConfig Hero() => new()
        {
            Key = "warrior_bran",
            Class = "warrior",
            BaseStats = new Dictionary<string, double> { ["Attack"] = 40, ["Defense"] = 60 },
            StatGrowth = new Dictionary<string, double> { ["Attack"] = 4, ["Defense"] = 7 }
        };

        // ---------- Стеля рівня ----------

        // ---------- Досвід ----------

        /// <summary>Стеля одна для всіх героїв — ні ратуша, ні тір її більше не задають (GDD §6.1).</summary>
        [Fact]
        public void MaxLevel_ShouldComeFromTheConfig()
            => Assert.Equal(80, Progression(new HeroesConfig { MaxLevel = 80 }).MaxLevel);

        /// <summary>Крива: ExperienceBase × L^ExperienceExponent — кожен наступний рівень дорожчий.</summary>
        [Fact]
        public void ExperienceToNext_ShouldFollowThePowerCurve()
        {
            var progression = Progression(new HeroesConfig { ExperienceBase = 10, ExperienceExponent = 2 });

            Assert.Equal(10, progression.ExperienceToNext(1));
            Assert.Equal(40, progression.ExperienceToNext(2));
            Assert.Equal(1000, progression.ExperienceToNext(10));
        }

        [Fact]
        public void ExperienceBetween_ShouldSumEveryStep()
        {
            var progression = Progression(new HeroesConfig { ExperienceBase = 10, ExperienceExponent = 2 });

            // 1→2: 10, 2→3: 40, 3→4: 90
            Assert.Equal(140, progression.ExperienceBetween(1, 4));
            Assert.Equal(0, progression.ExperienceBetween(4, 4));
        }

        /// <summary>Скидання повертає все вкладене до одиниці — досвід не згорає.</summary>
        [Fact]
        public void ResetRefund_ShouldReturnAllTheInvestedExperience()
        {
            var progression = Progression(new HeroesConfig { ExperienceBase = 10, ExperienceExponent = 2 });

            // 10×1² + 10×2² + 10×3² = 140
            Assert.Equal(140, progression.ResetRefund(level: 4));
            Assert.Equal(0, progression.ResetRefund(level: 1));
        }

        // ---------- Множник тіру ----------

        /// <summary>Рідний тір — складний відсоток (GDD §6.1): T2 = 1.10, T3 = 1.21.</summary>
        [Fact]
        public void TierMultiplier_ShouldCompoundForNativeHeroes()
        {
            var progression = Create();

            Assert.Equal(1.0, progression.TierMultiplier(tier: 1, nativeTier: 1), 6);
            Assert.Equal(1.10, progression.TierMultiplier(tier: 2, nativeTier: 2), 6);
            Assert.Equal(1.21, progression.TierMultiplier(tier: 3, nativeTier: 3), 6);
        }

        /// <summary>
        /// Кожен ап — ще −5%, і штраф накопичується: T1→T2 = 1.045, T1→T3 = 1.092 (рідний T3 — 1.21).
        /// Старий герой лишається в грі, але новий того самого тіру завжди сильніший.
        /// </summary>
        [Fact]
        public void TierMultiplier_ShouldPenaliseEveryEvolutionStep()
        {
            var progression = Create();

            Assert.Equal(1.045, progression.TierMultiplier(tier: 2, nativeTier: 1), 6);
            Assert.Equal(1.21 * 0.95, progression.TierMultiplier(tier: 3, nativeTier: 2), 6);
            Assert.Equal(1.21 * 0.9025, progression.TierMultiplier(tier: 3, nativeTier: 1), 6);
        }

        /// <summary>Ап усе одно сильніший за тір, з якого герой вийшов, — інакше предмет апу нічого б не давав.</summary>
        [Fact]
        public void TierMultiplier_ShouldStillReward_AnEvolution()
        {
            var progression = Create();

            Assert.True(progression.TierMultiplier(tier: 2, nativeTier: 1) > progression.TierMultiplier(tier: 1, nativeTier: 1));
            Assert.True(progression.TierMultiplier(tier: 3, nativeTier: 1) > progression.TierMultiplier(tier: 2, nativeTier: 1));
        }

        // ---------- Стати ----------

        /// <summary>
        /// Множник тіру діє і на базу, і на приріст: інакше високий тір
        /// знецінювався б із кожним новим рівнем.
        /// </summary>
        [Fact]
        public void StatValue_ShouldApplyGrowthThenTierMultiplier()
        {
            var progression = Create(tierGrowth: 2.0);

            // (40 + 4 × 4) × 2.0
            Assert.Equal(112.0, progression.StatValue(Hero(), "Attack", level: 5, tier: 2, nativeTier: 2, starParts: 0), 6);
        }

        [Fact]
        public void StatValue_ShouldReturnBaseAtFirstLevel()
        {
            var progression = Create(tierGrowth: 2.0);

            Assert.Equal(40.0, progression.StatValue(Hero(), "Attack", level: 1, tier: 1, nativeTier: 1, starParts: 0), 6);
        }

        /// <summary>
        /// Невідомий стат — нуль, не виняток: набір статів живе в конфігу,
        /// і бій не має падати від того, що герой не має якогось із них.
        /// </summary>
        [Fact]
        public void StatValue_ShouldReturnZero_ForAnUnknownStat()
        {
            var progression = Create();

            Assert.Equal(0.0, progression.StatValue(Hero(), "Mana", level: 5, tier: 1, nativeTier: 1, starParts: 0));
        }

        /// <summary>Кожна частинка зірки — +5% до всіх статів (GDD §6.1); множиться з тіром.</summary>
        [Fact]
        public void StatValue_ShouldGrowWithEveryStarPart()
        {
            var progression = Create(tierGrowth: 2.0);

            // 40 × 1.0 (тір 1) × (1 + 0.05 × 6)
            Assert.Equal(52.0, progression.StatValue(Hero(), "Attack", level: 1, tier: 1, nativeTier: 1, starParts: 6), 6);
        }

        [Fact]
        public void NextStarPartCost_ShouldWalkTheStarsAndStopAtTheEnd()
        {
            var progression = Progression(new HeroesConfig
            {
                MaxStars = 2, PartsPerStar = 2, StarPartCosts = [[1, 3], [10, 100]]
            });

            Assert.Equal(1, progression.NextStarPartCost(0));
            Assert.Equal(3, progression.NextStarPartCost(1));
            Assert.Equal(10, progression.NextStarPartCost(2));
            Assert.Equal(100, progression.NextStarPartCost(3));
            Assert.Null(progression.NextStarPartCost(4));
            Assert.Equal(1, progression.Stars(3));
        }

        // ---------- Еволюція ----------

        /// <summary>
        /// Тір прив'язаний до рівня світу: контент відкривається для всіх
        /// одночасно, а не для тих, хто швидше клікає.
        /// </summary>
        [Fact]
        public void CanEvolve_ShouldRequireTheMatchingServerLevel()
        {
            var progression = Create();

            Assert.False(progression.CanEvolve(currentTier: 1, serverLevel: 1));
            Assert.True(progression.CanEvolve(currentTier: 1, serverLevel: 2));
            Assert.False(progression.CanEvolve(currentTier: 2, serverLevel: 2));
            Assert.True(progression.CanEvolve(currentTier: 2, serverLevel: 3));
        }

        [Fact]
        public void CanEvolve_ShouldRefuseAtTheHighestTier()
        {
            var progression = Create();

            Assert.False(progression.CanEvolve(currentTier: 3, serverLevel: 9));
        }

        [Fact]
        public void EvolutionItemKey_ShouldReturnTheItemForTheNextStep()
        {
            var progression = Create();

            Assert.Equal("hero_essence_t2", progression.EvolutionItemKey(1));
            Assert.Equal("hero_essence_t3", progression.EvolutionItemKey(2));
        }

        [Fact]
        public void EvolutionItemKey_ShouldReturnNull_BeyondTheConfiguredSteps()
        {
            var progression = Create();

            Assert.Null(progression.EvolutionItemKey(3));
            Assert.Null(progression.EvolutionItemKey(0));
        }

        // ---------- Марші ----------

        /// <summary>
        /// Окремого лічильника слотів немає: один герой веде один марш,
        /// тож межа й так дорівнює ростеру.
        /// </summary>
        [Fact]
        public void MarchCapacity_ShouldFollowHeroCountBelowTheCap()
        {
            var progression = Create(maxMarches: 8);

            Assert.Equal(0, progression.MarchCapacity(heroCount: 0));
            Assert.Equal(3, progression.MarchCapacity(heroCount: 3));
        }

        [Fact]
        public void MarchCapacity_ShouldClampAtTheConfiguredCap()
        {
            var progression = Create(maxMarches: 8);

            Assert.Equal(8, progression.MarchCapacity(heroCount: 20));
        }

        // ---------- Час ----------

        [Fact]
        public void HealCost_ShouldScaleWithLevel()
        {
            var progression = Progression();

            var cost = progression.HealCost(level: 10);

            Assert.Equal(400, cost.Single(c => c.Resource == "food").Amount);
        }

        /// <summary>
        /// Тір множить бойові стати, але не швидкість: еволюція робить
        /// героя сильнішим, а не прудкішим, інакше карта стискалася б
        /// разом із прогресом.
        /// </summary>
        [Fact]
        public void MarchSpeed_ShouldNotScaleWithTier()
        {
            var config = Hero();
            config.Speed = 5;

            Assert.Equal(5, Progression().MarchSpeed(config), 3);
        }

        /// <summary>Тип без явної швидкості бере значення з налаштувань.</summary>
        [Fact]
        public void MarchSpeed_ShouldFallBackToTheDefault()
            => Assert.Equal(Settings().DefaultMarchSpeed, Progression().MarchSpeed(Hero()), 3);
    }
}
