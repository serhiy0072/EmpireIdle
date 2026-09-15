using EmpireIdle.Domain.Entities;
using EmpireIdle.Domain.Enums;
using EmpireIdle.Domain.Services;
using EmpireIdle.Domain.Services.Config;

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
        private static readonly DateTime Now = new(2026, 6, 1, 12, 0, 0, DateTimeKind.Utc);

        private static HeroStats Stats()
        {
            var config = HeroFixture.Config();

            return new HeroStats(new HeroProgression(config.HeroSettings), new GameCatalog(config));
        }

        private static HeroConfig HeroConfig()
            => new GameCatalog(HeroFixture.Config()).Hero(HeroFixture.Plain);

        private static EquipmentItem SetPiece(string key)
            => HeroFixture.Item(key, EquipmentSlot.Artifact, ("Attack", 5.0));

        // ---------- Власні стати ----------

        [Fact]
        public void Compute_ShouldReturnBareStats_WhenNothingIsEquipped()
        {
            var result = Stats().Compute(HeroFixture.HeroAt(level: 1), HeroConfig(), []);

            Assert.Equal(100, result["Attack"], 3);
            Assert.Equal(40, result["Defense"], 3);
        }

        [Fact]
        public void Compute_ShouldGrowWithLevel()
        {
            var result = Stats().Compute(HeroFixture.HeroAt(level: 5), HeroConfig(), []);

            // 100 + 10 × 4
            Assert.Equal(140, result["Attack"], 3);
        }

        [Fact]
        public void Compute_ShouldMultiplyOwnStatsByTier()
        {
            var result = Stats().Compute(HeroFixture.HeroAt(level: 1, tier: 2), HeroConfig(), []);

            Assert.Equal(150, result["Attack"], 3);
        }

        // ---------- Спорядження ----------

        [Fact]
        public void Compute_ShouldAddEquipmentStats()
        {
            var sword = HeroFixture.Item(HeroFixture.Weapon, EquipmentSlot.Weapon, ("Attack", 12.0));

            var result = Stats().Compute(HeroFixture.HeroAt(level: 1), HeroConfig(), [sword]);

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

            var bareTier1 = stats.Compute(HeroFixture.HeroAt(tier: 1), config, [])["Attack"];
            var bareTier3 = stats.Compute(HeroFixture.HeroAt(tier: 3), config, [])["Attack"];

            var sword = HeroFixture.Item(HeroFixture.Weapon, EquipmentSlot.Weapon, ("Attack", 12.0));

            var withTier1 = stats.Compute(HeroFixture.HeroAt(tier: 1), config, [sword])["Attack"];
            var withTier3 = stats.Compute(HeroFixture.HeroAt(tier: 3), config, [sword])["Attack"];

            Assert.Equal(12, withTier1 - bareTier1, 3);
            Assert.Equal(12, withTier3 - bareTier3, 3);
        }

        [Fact]
        public void Compute_ShouldCountEnhancementInEquipmentStats()
        {
            var sword = HeroFixture.Item(HeroFixture.Weapon, EquipmentSlot.Weapon, ("Attack", 10.0));
            sword.Enhance(Now);
            sword.Enhance(Now);

            var result = Stats().Compute(HeroFixture.HeroAt(), HeroConfig(), [sword]);

            // 10 × (1 + 2 × 0.1)
            Assert.Equal(112, result["Attack"], 3);
        }

        /// <summary>Зламане не дає нічого — так само, як і в бою.</summary>
        [Fact]
        public void Compute_ShouldIgnoreBrokenEquipment()
        {
            var sword = HeroFixture.Item(HeroFixture.Weapon, EquipmentSlot.Weapon, ("Attack", 12.0));
            sword.Break(Now);

            var result = Stats().Compute(HeroFixture.HeroAt(), HeroConfig(), [sword]);

            Assert.Equal(100, result["Attack"], 3);
        }

        /// <summary>Стат, якого немає в героя, приходить зі спорядження цілим.</summary>
        [Fact]
        public void Compute_ShouldIntroduceStatsTheHeroLacks()
        {
            var charm = HeroFixture.Item(HeroFixture.LooseArtifact, EquipmentSlot.Artifact, ("Health", 50.0));

            var result = Stats().Compute(HeroFixture.HeroAt(), HeroConfig(), [charm]);

            Assert.Equal(50, result["Health"], 3);
        }

        [Fact]
        public void Compute_ShouldSumSeveralItems()
        {
            var sword = HeroFixture.Item(HeroFixture.Weapon, EquipmentSlot.Weapon, ("Attack", 12.0));
            var charm = HeroFixture.Item(HeroFixture.LooseArtifact, EquipmentSlot.Artifact, ("Attack", 6.0));

            var result = Stats().Compute(HeroFixture.HeroAt(), HeroConfig(), [sword, charm]);

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
                SetPiece(HeroFixture.SetPiece1),
                SetPiece(HeroFixture.SetPiece2),
                SetPiece(HeroFixture.SetPiece3)
            ]);

            Assert.Empty(bonus);
        }

        [Fact]
        public void SetBonus_ShouldApply_OnTheFullSet()
        {
            var bonus = Stats().SetBonus(
            [
                SetPiece(HeroFixture.SetPiece1),
                SetPiece(HeroFixture.SetPiece2),
                SetPiece(HeroFixture.SetPiece3),
                SetPiece(HeroFixture.SetPiece4)
            ]);

            Assert.Equal(HeroFixture.SetBonusAttack, bonus["Attack"], 3);
        }

        /// <summary>Зламаний артефакт комплект не закриває.</summary>
        [Fact]
        public void SetBonus_ShouldNotCountBrokenPieces()
        {
            var broken = SetPiece(HeroFixture.SetPiece4);
            broken.Break(Now);

            var bonus = Stats().SetBonus(
            [
                SetPiece(HeroFixture.SetPiece1),
                SetPiece(HeroFixture.SetPiece2),
                SetPiece(HeroFixture.SetPiece3),
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
                SetPiece(HeroFixture.SetPiece1),
                SetPiece(HeroFixture.SetPiece2),
                SetPiece(HeroFixture.SetPiece3),
                SetPiece(HeroFixture.LooseArtifact)
            ]);

            Assert.Empty(bonus);
        }

        /// <summary>Повний набір входить у підсумкові стати, а не живе окремо.</summary>
        [Fact]
        public void Compute_ShouldIncludeTheSetBonus()
        {
            var result = Stats().Compute(HeroFixture.HeroAt(), HeroConfig(),
            [
                SetPiece(HeroFixture.SetPiece1),
                SetPiece(HeroFixture.SetPiece2),
                SetPiece(HeroFixture.SetPiece3),
                SetPiece(HeroFixture.SetPiece4)
            ]);

            // 100 базових + 4 × 5 зі статів + 25 за комплект
            Assert.Equal(145, result["Attack"], 3);
        }
    }
}
