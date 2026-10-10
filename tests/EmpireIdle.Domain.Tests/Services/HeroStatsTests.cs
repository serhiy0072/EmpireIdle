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
    /// готового числа додається спорядження. Множник тіру героя на екіп не діє,
    /// інакше той самий артефакт на третьому тірі коштував би вдвічі більше,
    /// ніж на першому, і сенс шукати кращий зникав би.
    ///
    /// Звичайний артефакт рівня 0 у TestKit: атака й захист героя по 10, юнітів — по 40.
    /// Воїн (TestKeys.CommonHero) має множник класу 1.0 атаки й 1.2 захисту.
    /// </summary>
    public class HeroStatsTests
    {
        private static HeroStats Stats()
        {
            var config = new GameConfigBuilder()
                .WithHeroes()
                .WithEquipment(e => e.ArtifactClassMultipliers["warrior"] = new ArtifactClassMultiplierConfig { Attack = 1.0, Defense = 1.2 })
                .Build();

            return new HeroStats(new HeroProgression(config.HeroSettings), new GameCatalog(config));
        }

        private static HeroConfig HeroConfig()
            => new GameCatalog(new GameConfigBuilder().WithHeroes().WithEquipment().Build()).Hero(TestKeys.CommonHero);

        private static EquipmentItem SetPiece(string key)
            => TestKit.Entities.Equipment(key, EquipmentSlot.Artifact);

        private static EquipmentItem Necklace(int level = 0, params (int Position, string Stat, double Value)[] bonuses)
            => TestKit.Entities.Equipment(TestKeys.Artifact, EquipmentSlot.Artifact, level: level, bonuses: bonuses);

        // ---------- Сила ----------

        /// <summary>Сила героя — сума власних статів: 100 атаки + 40 захисту на першому рівні.</summary>
        [Fact]
        public void Power_ShouldSumTheHerosOwnStats()
            => Assert.Equal(140, Stats().Power(TestKit.Entities.Hero(TestKeys.CommonHero, level: 1), HeroConfig()), 3);

        /// <summary>
        /// Найсильніший — першим (GDD §6.1): він обличчя маршу й бере слот лідера на прибутті.
        /// Невідомий довіднику герой — у кінці, а не виняток.
        /// </summary>
        [Fact]
        public void StrongestFirst_ShouldPutTheStrongestHeroFirst()
        {
            var weak = TestKit.Entities.Hero(TestKeys.CommonHero, level: 1);
            var strong = TestKit.Entities.Hero(TestKeys.CommonHero, level: 5);
            var unknown = TestKit.Entities.Hero("retired_hero", level: 10);

            var ordered = Stats().StrongestFirst([unknown, weak, strong]);

            Assert.Equal([strong, weak, unknown], ordered);
        }

        /// <summary>Сила росте з рівнем так само, як стати: (100 + 10 × 4) + (40 + 4 × 4).</summary>
        [Fact]
        public void Power_ShouldGrowWithTheHerosLevel()
            => Assert.Equal(196, Stats().Power(TestKit.Entities.Hero(TestKeys.CommonHero, level: 5), HeroConfig()), 3);

        /// <summary>Сила звичайного артефакта рівня 0: (10 + 10) × 1 + (40 + 40) × 0.25.</summary>
        [Fact]
        public void Power_ShouldWeighTheItemsFlatBase()
            => Assert.Equal(40, Stats().Power(Necklace()), 3);

        /// <summary>
        /// Рівень 2 і бонуси: атака 14 × 1.1, захист 14, атака юнітів 56 × 1.05, захист юнітів 56,
        /// плюс 2% крита по 10 сили: 29.4 + 114.8 × 0.25 + 20.
        /// </summary>
        [Fact]
        public void Power_ShouldCountTheLevelTheFixedBoostsAndTheRandomBonuses()
        {
            var item = Necklace(level: 2, (0, "Attack", 10), (1, "UnitAttack", 5), (2, "CritChance", 2));

            Assert.Equal(78.1, Stats().Power(item), 3);
        }

        /// <summary>
        /// Сила героя з вдягненим — власна плюс сила предмета з множником класу:
        /// 140 + (10 + 12) + (40 + 48) × 0.25.
        /// </summary>
        [Fact]
        public void Power_WithGear_ShouldAddTheItemPowerWithTheClassMultiplier()
            => Assert.Equal(184, Stats().Power(TestKit.Entities.Hero(TestKeys.CommonHero, level: 1), HeroConfig(), [Necklace()]), 3);

        /// <summary>Відсотки крита складаються в силу за своєю вагою, а не з пласкою атакою.</summary>
        [Fact]
        public void Power_WithGear_ShouldNotSumPercentStatsAsFlatStats()
        {
            var plain = Stats().Power(TestKit.Entities.Hero(TestKeys.CommonHero), HeroConfig(), [Necklace()]);
            var critical = Stats().Power(TestKit.Entities.Hero(TestKeys.CommonHero), HeroConfig(),
                [Necklace(0, (2, "CritChance", 3))]);

            Assert.Equal(30, critical - plain, 3);
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

        /// <summary>Рідний T2 — повний ріст тіру (у TestKit ×1.5).</summary>
        [Fact]
        public void Compute_ShouldMultiplyOwnStatsByTier()
        {
            var result = Stats().Compute(TestKit.Entities.Hero(TestKeys.CommonHero, level: 1, tier: 2, nativeTier: 2), HeroConfig(), []);

            Assert.Equal(150, result["Attack"], 3);
        }

        /// <summary>Піднятий з T1 до T2 — той самий ріст мінус 5% за ап (GDD §6.1): 100 × 1.5 × 0.95.</summary>
        [Fact]
        public void Compute_ShouldPenaliseAnEvolvedHero()
        {
            var result = Stats().Compute(TestKit.Entities.Hero(TestKeys.CommonHero, level: 1, tier: 2), HeroConfig(), []);

            Assert.Equal(142.5, result["Attack"], 3);
        }

        // ---------- Спорядження ----------

        /// <summary>База предмета йде в стати з множником класу: воїн отримує 10 атаки й 12 захисту.</summary>
        [Fact]
        public void Compute_ShouldAddTheItemBase_WithTheClassMultiplier()
        {
            var result = Stats().Compute(TestKit.Entities.Hero(TestKeys.CommonHero, level: 1), HeroConfig(), [Necklace()]);

            Assert.Equal(110, result["Attack"], 3);
            Assert.Equal(52, result["Defense"], 3);
            Assert.Equal(40, result["UnitAttack"], 3);
            Assert.Equal(48, result["UnitDefense"], 3);
        }

        /// <summary>
        /// Той самий артефакт дає однаково на першому й третьому тірі. Це головна
        /// перевірка порядку: якби екіп множився тіром героя, різниця була б удвічі.
        /// </summary>
        [Fact]
        public void Compute_ShouldNotScaleEquipmentWithTier()
        {
            var stats = Stats();
            var config = HeroConfig();

            var bareTier1 = stats.Compute(TestKit.Entities.Hero(TestKeys.CommonHero, tier: 1), config, [])["Attack"];
            var bareTier3 = stats.Compute(TestKit.Entities.Hero(TestKeys.CommonHero, tier: 3), config, [])["Attack"];

            var withTier1 = stats.Compute(TestKit.Entities.Hero(TestKeys.CommonHero, tier: 1), config, [Necklace()])["Attack"];
            var withTier3 = stats.Compute(TestKit.Entities.Hero(TestKeys.CommonHero, tier: 3), config, [Necklace()])["Attack"];

            Assert.Equal(10, withTier1 - bareTier1, 3);
            Assert.Equal(10, withTier3 - bareTier3, 3);
        }

        /// <summary>Рівень 2 і сталий бонус +10% атаки: (10 + 2 × 2) × 1.1.</summary>
        [Fact]
        public void Compute_ShouldCountTheLevelAndTheFixedBoost()
        {
            var result = Stats().Compute(TestKit.Entities.Hero(TestKeys.CommonHero), HeroConfig(),
                [Necklace(level: 2, (0, "Attack", 10))]);

            Assert.Equal(115.4, result["Attack"], 3);
        }

        /// <summary>Відсоток атаки героя множить усю атаку, а не лише ту, що дав предмет: (100 + 10) × 1.1.</summary>
        [Fact]
        public void Compute_ShouldMultiplyTheWholeStat_ByAHeroPercentBonus()
        {
            var result = Stats().Compute(TestKit.Entities.Hero(TestKeys.CommonHero), HeroConfig(),
                [Necklace(0, (2, "AttackPercent", 10))]);

            Assert.Equal(121, result["Attack"], 3);
        }

        /// <summary>Бонус, якого бій ще не знає, приходить у стати своїм ключем — для сили й картки героя.</summary>
        [Fact]
        public void Compute_ShouldCarryRandomBonusesUnderTheirKeys()
        {
            var result = Stats().Compute(TestKit.Entities.Hero(TestKeys.CommonHero), HeroConfig(),
                [Necklace(0, (2, "CritChance", 3)), TestKit.Entities.Equipment(TestKeys.SecondArtifact, EquipmentSlot.Artifact,
                    bonuses: [(2, "CritChance", 2)])]);

            Assert.Equal(5, result["CritChance"], 3);
        }

        [Fact]
        public void Compute_ShouldSumSeveralItems()
        {
            var ring = TestKit.Entities.Equipment(TestKeys.LooseArtifact, EquipmentSlot.Artifact);

            var result = Stats().Compute(TestKit.Entities.Hero(TestKeys.CommonHero), HeroConfig(), [Necklace(), ring]);

            Assert.Equal(120, result["Attack"], 3);
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

            // 100 базових + 4 × 10 бази артефактів + 25 за комплект
            Assert.Equal(165, result["Attack"], 3);
        }
    }
}
