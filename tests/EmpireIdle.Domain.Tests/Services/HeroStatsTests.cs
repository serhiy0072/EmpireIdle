using EmpireIdle.Domain.Entities;
using EmpireIdle.Domain.Enums;
using EmpireIdle.Domain.Services;
using EmpireIdle.Domain.Services.Config;
using EmpireIdle.TestKit;

namespace EmpireIdle.Domain.Tests.Services
{
    /// <summary>
    /// Підсумкові стати героя.
    ///
    /// Головне тут — порядок: рівень і тір дають стат героя, і вже до
    /// готового числа додається спорядження. Множник тіру на екіп не діє,
    /// інакше той самий меч на третьому тірі коштував би вдвічі більше,
    /// ніж на першому, і сенс шукати кращий зникав би.
    /// </summary>
    public class HeroStatsTests
    {
        private static HeroStats Stats()
        {
            var config = new GameConfigBuilder()
                .WithHeroes()
                .WithEquipment()
                .Build();

            return new HeroStats(new HeroProgression(config.HeroSettings), new GameCatalog(config));
        }

        private static HeroConfig HeroConfig()
            => new GameCatalog(new GameConfigBuilder().WithHeroes().WithEquipment().Build()).Hero(TestKeys.CommonHero);

        private static EquipmentItem SetPiece(string key)
            => TestKit.Entities.Equipment(key, EquipmentSlot.Artifact, stats: [("Attack", 5.0)]);

        // ---------- Сила ----------

        /// <summary>Сила героя — сума власних статів: 100 атаки + 40 захисту на першому рівні.</summary>
        [Fact]
        public void Power_ShouldSumTheHerosOwnStats()
            => Assert.Equal(140, Stats().Power(TestKit.Entities.Hero(TestKeys.CommonHero, level: 1), HeroConfig()), 3);

        /// <summary>Сила росте з рівнем так само, як стати: (100 + 10 × 4) + (40 + 4 × 4).</summary>
        [Fact]
        public void Power_ShouldGrowWithTheHerosLevel()
            => Assert.Equal(196, Stats().Power(TestKit.Entities.Hero(TestKeys.CommonHero, level: 5), HeroConfig()), 3);

        /// <summary>Сила предмета — сума статів із заточкою: (5 + 3) × (1 + 2 × 0.1).</summary>
        [Fact]
        public void Power_ShouldSumAnItemsStatsWithEnhancement()
        {
            var item = TestKit.Entities.Equipment(TestKeys.Artifact, EquipmentSlot.Artifact,
                stats: [("Attack", 5.0), ("Defense", 3.0)]);
            item.Enhance(TestKit.Entities.Now);
            item.Enhance(TestKit.Entities.Now);

            Assert.Equal(9.6, Stats().Power(item), 3);
        }

        /// <summary>Зламаний предмет нічого не дає — і сили в нього немає.</summary>
        [Fact]
        public void Power_ShouldBeZero_ForABrokenItem()
        {
            var item = TestKit.Entities.Equipment(TestKeys.Artifact, EquipmentSlot.Artifact, stats: [("Attack", 5.0)]);
            item.Break(TestKit.Entities.Now);

            Assert.Equal(0, Stats().Power(item), 3);
        }

        // ---------- Власні стати ----------

        [Fact]
        public void Compute_ShouldReturnBareStats_WhenNothingIsEquipped()
        {
            var result = Stats().Compute(TestKit.Entities.Hero(TestKeys.CommonHero, level: 1), HeroConfig(), []);

            Assert.Equal(100, result["Attack"], 3);
            Assert.Equal(40, result["Defense"], 3);
        }

        [Fact]
        public void Compute_ShouldGrowWithLevel()
        {
            var result = Stats().Compute(TestKit.Entities.Hero(TestKeys.CommonHero, level: 5), HeroConfig(), []);

            // 100 + 10 × 4
            Assert.Equal(140, result["Attack"], 3);
        }

        [Fact]
        public void Compute_ShouldMultiplyOwnStatsByTier()
        {
            var result = Stats().Compute(TestKit.Entities.Hero(TestKeys.CommonHero, level: 1, tier: 2), HeroConfig(), []);

            Assert.Equal(150, result["Attack"], 3);
        }

        // ---------- Спорядження ----------

        [Fact]
        public void Compute_ShouldAddEquipmentStats()
        {
            var sword = TestKit.Entities.Equipment(TestKeys.Weapon, EquipmentSlot.Weapon, stats: [("Attack", 12.0)]);

            var result = Stats().Compute(TestKit.Entities.Hero(TestKeys.CommonHero, level: 1), HeroConfig(), [sword]);

            Assert.Equal(112, result["Attack"], 3);
        }

        /// <summary>
        /// Той самий меч дає однаково на першому й третьому тірі. Це головна
        /// перевірка порядку: якби екіп множився тіром, різниця була б удвічі.
        /// </summary>
        [Fact]
        public void Compute_ShouldNotScaleEquipmentWithTier()
        {
            var stats = Stats();
            var config = HeroConfig();

            var bareTier1 = stats.Compute(TestKit.Entities.Hero(TestKeys.CommonHero, tier: 1), config, [])["Attack"];
            var bareTier3 = stats.Compute(TestKit.Entities.Hero(TestKeys.CommonHero, tier: 3), config, [])["Attack"];

            var sword = TestKit.Entities.Equipment(TestKeys.Weapon, EquipmentSlot.Weapon, stats: [("Attack", 12.0)]);

            var withTier1 = stats.Compute(TestKit.Entities.Hero(TestKeys.CommonHero, tier: 1), config, [sword])["Attack"];
            var withTier3 = stats.Compute(TestKit.Entities.Hero(TestKeys.CommonHero, tier: 3), config, [sword])["Attack"];

            Assert.Equal(12, withTier1 - bareTier1, 3);
            Assert.Equal(12, withTier3 - bareTier3, 3);
        }

        [Fact]
        public void Compute_ShouldCountEnhancementInEquipmentStats()
        {
            var sword = TestKit.Entities.Equipment(TestKeys.Weapon, EquipmentSlot.Weapon, stats: [("Attack", 10.0)]);
            sword.Enhance(TestKit.Entities.Now);
            sword.Enhance(TestKit.Entities.Now);

            var result = Stats().Compute(TestKit.Entities.Hero(TestKeys.CommonHero), HeroConfig(), [sword]);

            // 10 × (1 + 2 × 0.1)
            Assert.Equal(112, result["Attack"], 3);
        }

        /// <summary>Зламане не дає нічого — так само, як і в бою.</summary>
        [Fact]
        public void Compute_ShouldIgnoreBrokenEquipment()
        {
            var sword = TestKit.Entities.Equipment(TestKeys.Weapon, EquipmentSlot.Weapon, stats: [("Attack", 12.0)]);
            sword.Break(TestKit.Entities.Now);

            var result = Stats().Compute(TestKit.Entities.Hero(TestKeys.CommonHero), HeroConfig(), [sword]);

            Assert.Equal(100, result["Attack"], 3);
        }

        /// <summary>Стат, якого немає в героя, приходить зі спорядження цілим.</summary>
        [Fact]
        public void Compute_ShouldIntroduceStatsTheHeroLacks()
        {
            var charm = TestKit.Entities.Equipment(TestKeys.LooseArtifact, EquipmentSlot.Artifact, stats: [("Health", 50.0)]);

            var result = Stats().Compute(TestKit.Entities.Hero(TestKeys.CommonHero), HeroConfig(), [charm]);

            Assert.Equal(50, result["Health"], 3);
        }

        [Fact]
        public void Compute_ShouldSumSeveralItems()
        {
            var sword = TestKit.Entities.Equipment(TestKeys.Weapon, EquipmentSlot.Weapon, stats: [("Attack", 12.0)]);
            var charm = TestKit.Entities.Equipment(TestKeys.LooseArtifact, EquipmentSlot.Artifact, stats: [("Attack", 6.0)]);

            var result = Stats().Compute(TestKit.Entities.Hero(TestKeys.CommonHero), HeroConfig(), [sword, charm]);

            Assert.Equal(118, result["Attack"], 3);
        }

        // ---------- Набір ----------

        [Fact]
        public void SetBonus_ShouldGiveNothing_WhenNothingIsEquipped()
            => Assert.Empty(Stats().SetBonus([]));

        /// <summary>
        /// Три з чотирьох не дають нічого. Саме це й робить набір метою,
        /// а не побічним ефектом збирання найсильніших артефактів.
        /// </summary>
        [Fact]
        public void SetBonus_ShouldGiveNothing_BelowTheFullSet()
        {
            var bonus = Stats().SetBonus(
            [
                SetPiece(TestKeys.Artifact),
                SetPiece(TestKeys.SecondArtifact),
                SetPiece(TestKeys.ThirdArtifact)
            ]);

            Assert.Empty(bonus);
        }

        [Fact]
        public void SetBonus_ShouldApply_OnTheFullSet()
        {
            var bonus = Stats().SetBonus(
            [
                SetPiece(TestKeys.Artifact),
                SetPiece(TestKeys.SecondArtifact),
                SetPiece(TestKeys.ThirdArtifact),
                SetPiece(TestKeys.FourthArtifact)
            ]);

            Assert.Equal(25, bonus["Attack"], 3);
        }

        /// <summary>Зламаний артефакт комплект не закриває.</summary>
        [Fact]
        public void SetBonus_ShouldNotCountBrokenPieces()
        {
            var broken = SetPiece(TestKeys.FourthArtifact);
            broken.Break(TestKit.Entities.Now);

            var bonus = Stats().SetBonus(
            [
                SetPiece(TestKeys.Artifact),
                SetPiece(TestKeys.SecondArtifact),
                SetPiece(TestKeys.ThirdArtifact),
                broken
            ]);

            Assert.Empty(bonus);
        }

        /// <summary>Артефакт поза набором комплект не добирає.</summary>
        [Fact]
        public void SetBonus_ShouldNotCountForeignItems()
        {
            var bonus = Stats().SetBonus(
            [
                SetPiece(TestKeys.Artifact),
                SetPiece(TestKeys.SecondArtifact),
                SetPiece(TestKeys.ThirdArtifact),
                SetPiece(TestKeys.LooseArtifact)
            ]);

            Assert.Empty(bonus);
        }

        /// <summary>Повний набір входить у підсумкові стати, а не живе окремо.</summary>
        [Fact]
        public void Compute_ShouldIncludeTheSetBonus()
        {
            var result = Stats().Compute(TestKit.Entities.Hero(TestKeys.CommonHero), HeroConfig(),
            [
                SetPiece(TestKeys.Artifact),
                SetPiece(TestKeys.SecondArtifact),
                SetPiece(TestKeys.ThirdArtifact),
                SetPiece(TestKeys.FourthArtifact)
            ]);

            // 100 базових + 4 × 5 зі статів + 25 за комплект
            Assert.Equal(145, result["Attack"], 3);
        }
    }
}
