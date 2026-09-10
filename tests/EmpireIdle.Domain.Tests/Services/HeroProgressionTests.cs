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
            int maxTier = 3,
            int maxMarches = 8,
            List<double>? multipliers = null,
            List<string>? evolutionItems = null)
            => new(new HeroesConfig
            {
                LevelsPerTier = levelsPerTier,
                MaxTier = maxTier,
                MaxMarches = maxMarches,
                TierStatMultipliers = multipliers ?? [1.0, 1.35, 1.8],
                EvolutionItemKeys = evolutionItems ?? ["hero_essence_t2", "hero_essence_t3"],
                HealMinutesPerLevel = 3,
                BaseLevelUpMinutes = 4
            });

        private static HeroConfig Hero() => new()
        {
            Key = "warrior_bran",
            Class = "warrior",
            BaseStats = new Dictionary<string, double> { ["Attack"] = 40, ["Defense"] = 60 },
            StatGrowth = new Dictionary<string, double> { ["Attack"] = 4, ["Defense"] = 7 }
        };

        // ---------- Стеля рівня ----------

        /// <summary>
        /// Ратуша нижча за стелю тіру — вона й обмежує. Саме тому еволюція
        /// не обов'язкова для прогресу на ранніх ратушах.
        /// </summary>
        [Fact]
        public void MaxLevel_ShouldFollowTownHall_WhenItIsTheLowerBound()
        {
            var progression = Create();

            Assert.Equal(8, progression.MaxLevel(townHallLevel: 8, tier: 2));
        }

        [Fact]
        public void MaxLevel_ShouldFollowTier_WhenTownHallIsHigher()
        {
            var progression = Create();

            Assert.Equal(10, progression.MaxLevel(townHallLevel: 25, tier: 1));
            Assert.Equal(20, progression.MaxLevel(townHallLevel: 25, tier: 2));
        }

        // ---------- Множник тіру ----------

        /// <summary>
        /// Без множника еволюція піднімала б лише стелю, і два герої
        /// різних тірів на десятому рівні були б однакові.
        /// </summary>
        [Fact]
        public void TierMultiplier_ShouldGrowWithTier()
        {
            var progression = Create();

            Assert.True(progression.TierMultiplier(2) > progression.TierMultiplier(1));
            Assert.True(progression.TierMultiplier(3) > progression.TierMultiplier(2));
        }

        [Fact]
        public void TierMultiplier_ShouldFallBackToOne_WhenNotConfigured()
        {
            var progression = Create(multipliers: []);

            Assert.Equal(1.0, progression.TierMultiplier(2));
        }

        /// <summary>Коротший список не має валити бій — беремо останній відомий.</summary>
        [Fact]
        public void TierMultiplier_ShouldClampOutsideTheConfiguredRange()
        {
            var progression = Create(multipliers: [1.0, 1.5]);

            Assert.Equal(1.0, progression.TierMultiplier(0));
            Assert.Equal(1.5, progression.TierMultiplier(9));
        }

        // ---------- Стати ----------

        /// <summary>
        /// Множник тіру діє і на базу, і на приріст: інакше високий тір
        /// знецінювався б із кожним новим рівнем.
        /// </summary>
        [Fact]
        public void StatValue_ShouldApplyGrowthThenTierMultiplier()
        {
            var progression = Create(multipliers: [1.0, 2.0, 3.0]);

            // (40 + 4 × 4) × 2.0
            Assert.Equal(112.0, progression.StatValue(Hero(), "Attack", level: 5, tier: 2), 6);
        }

        [Fact]
        public void StatValue_ShouldReturnBaseAtFirstLevel()
        {
            var progression = Create(multipliers: [1.0, 2.0, 3.0]);

            Assert.Equal(40.0, progression.StatValue(Hero(), "Attack", level: 1, tier: 1), 6);
        }

        /// <summary>
        /// Невідомий стат — нуль, не виняток: набір статів живе в конфігу,
        /// і бій не має падати від того, що герой не має якогось із них.
        /// </summary>
        [Fact]
        public void StatValue_ShouldReturnZero_ForAnUnknownStat()
        {
            var progression = Create();

            Assert.Equal(0.0, progression.StatValue(Hero(), "Mana", level: 5, tier: 1));
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
        public void HealDuration_ShouldScaleWithLevel()
        {
            var progression = Create();

            Assert.Equal(TimeSpan.FromMinutes(30), progression.HealDuration(level: 10));
        }

        [Fact]
        public void LevelUpDuration_ShouldScaleWithTargetLevel()
        {
            var progression = Create();

            Assert.Equal(TimeSpan.FromMinutes(20), progression.LevelUpDuration(targetLevel: 5));
        }
    }
}
