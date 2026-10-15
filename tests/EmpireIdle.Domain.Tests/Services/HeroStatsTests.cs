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

        /// <summary>Кожен стат іде в силу зі своєю вагою: 100 атаки × 1 + 40 захисту × 0.5.</summary>
        [Fact]
        public void Power_ShouldWeighEachStat_WhenWeightsAreSet()
        {
            var config = new GameConfigBuilder().WithHeroes().WithEquipment().Build();
            config.HeroSettings.StatPowerWeights = new Dictionary<string, double> { ["Attack"] = 1.0, ["Defense"] = 0.5 };
            var catalog = new GameCatalog(config);
            var stats = new HeroStats(new HeroProgression(config.HeroSettings), catalog);

            var power = stats.Power(TestKit.Entities.Hero(TestKeys.CommonHero, level: 1), catalog.Hero(TestKeys.CommonHero));

            Assert.Equal(120, power, 3);
        }

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

        /// <summary>Зламаний дає половину сили: (20 + 80 × 0.25 + 2% крита × 10) / 2.</summary>
        [Fact]
        public void Power_ShouldHalve_ForABrokenItem()
        {
            var item = Necklace(0, (2, "CritChance", 2));
            item.Break(TestKit.Entities.Now);

            Assert.Equal(30, Stats().Power(item), 3);
        }

        /// <summary>Звичайний артефакт зі здоров'ям: 60 на нулі й +12 за рівень, ваги сили як у грі.</summary>
        private static (HeroStats Stats, HeroConfig Hero) WithArtifactHealth()
        {
            var config = new GameConfigBuilder()
                .WithHeroes()
                .WithEquipment(e =>
                {
                    e.ArtifactClassMultipliers["warrior"] = new ArtifactClassMultiplierConfig { Attack = 1.0, Defense = 1.2 };
                    e.ArtifactBase[Rarity.Common].HeroHealth = 60;
                    e.ArtifactBase[Rarity.Common].HeroHealthPerLevel = 12;
                })
                .Build();
            config.HeroSettings.StatPowerWeights = new Dictionary<string, double> { ["Attack"] = 1.0, ["Defense"] = 1.0, ["Health"] = 0.25 };
            var catalog = new GameCatalog(config);

            return (new HeroStats(new HeroProgression(config.HeroSettings), catalog), catalog.Hero(TestKeys.CommonHero));
        }

        /// <summary>
        /// Здоров'я артефакта росте з рівнем, а множник класу на нього не діє: 60 + 12 × 2 і на воїні.
        /// Без нього рана героя наставала б дедалі легше з прогресом.
        /// </summary>
        [Fact]
        public void Compute_ShouldAddTheArtifactsHealthWithoutTheClassMultiplier()
        {
            var (stats, hero) = WithArtifactHealth();

            var result = stats.Compute(TestKit.Entities.Hero(TestKeys.CommonHero, level: 1), hero, [Necklace(level: 2)]);

            Assert.Equal(84, result["Health"], 3);
        }

        /// <summary>
        /// Той самий предмет дає однакову силу будь-якому класу: атака й захист класів у сумі рівні,
        /// а здоров'я від класу не залежить. Інакше рідкісний воїн обганяв би унікального лучника.
        /// </summary>
        [Fact]
        public void Power_ShouldNotDependOnTheHerosClass_ForTheSameItem()
        {
            var config = new GameConfigBuilder().WithHeroes().WithEquipment(e =>
            {
                e.ArtifactBase[Rarity.Common].HeroHealth = 60;
                e.ArtifactBase[Rarity.Common].HeroHealthPerLevel = 12;
            }).Build();
            var artifacts = new ArtifactStats(new GameCatalog(config));
            var item = Necklace(level: 10);

            var warrior = artifacts.Power(item, new ArtifactClassMultiplierConfig { Attack = 1.0, Defense = 1.2 });
            var archer = artifacts.Power(item, new ArtifactClassMultiplierConfig { Attack = 1.4, Defense = 0.8 });

            Assert.Equal(warrior, archer, 3);
        }

        /// <summary>Здоров'я предмета важить у силі як здоров'я героя: (10 + 10) + (40 + 40) × 0.25 + 60 × 0.25.</summary>
        [Fact]
        public void Power_ShouldWeighTheArtifactsHealthLikeTheHerosOwn()
            => Assert.Equal(55, WithArtifactHealth().Stats.Power(Necklace()), 3);

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

        /// <summary>Зламаний урізає і базу, і відсотки: 10 / 2 атаки, 4% / 2 крита.</summary>
        [Fact]
        public void Compute_ShouldHalveABrokenItemsStats()
        {
            var item = Necklace(0, (2, "CritChance", 4));
            item.Break(TestKit.Entities.Now);

            var result = Stats().Compute(TestKit.Entities.Hero(TestKeys.CommonHero), HeroConfig(), [item]);

            Assert.Equal(105, result["Attack"], 3);
            Assert.Equal(2, result["CritChance"], 3);
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

        /// <summary>Три з чотирьох не дають порогу 4, а порогу 2 в цьому наборі немає.</summary>
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

        /// <summary>
        /// Родина «amber» у трьох рідкостях з порогами 2 і 4: числа порогу — це рідкість у тисячах
        /// (звичайна 1, рідкісна 2, унікальна 3) плюс поріг, щоб з атаки було видно, що спрацювало.
        /// </summary>
        private static HeroStats AmberStats()
        {
            var config = new GameConfigBuilder().WithHeroes().WithEquipment().Build();
            config.Equipment.ArtifactSets.Add(new ArtifactSetConfig { Key = "amber", DisplayName = "Набір Бурштину" });

            foreach (var rarity in Enum.GetValues<Rarity>())
            {
                var setKey = $"amber_{rarity.ToString().ToLowerInvariant()}";

                foreach (var slot in new[] { "necklace", "ring", "crown", "belt" })
                    config.Items.Add(new ItemConfig { Key = $"{setKey}_{slot}", Type = "equipment", Slot = EquipmentSlot.Artifact, ArtifactSlot = slot, SetKey = setKey });

                foreach (var pieces in new[] { 2, 4 })
                    config.Equipment.SetBonuses.Add(new SetBonusConfig { SetKey = setKey, RequiredPieces = pieces, Stats = new() { ["Attack"] = (int)rarity * 1000 + pieces } });
            }

            return new HeroStats(new HeroProgression(config.HeroSettings), new GameCatalog(config));
        }

        private static EquipmentItem Amber(Rarity rarity, string slot)
            => SetPiece($"amber_{rarity.ToString().ToLowerInvariant()}_{slot}");

        /// <summary>Три предмети родини — лише поріг 2: третій нового не додає.</summary>
        [Fact]
        public void SetBonus_ShouldGiveTheTwoPieceBonus_OnThreePieces()
        {
            var bonus = AmberStats().SetBonus([Amber(Rarity.Rare, "necklace"), Amber(Rarity.Rare, "ring"), Amber(Rarity.Rare, "crown")]);

            Assert.Equal(2002, bonus["Attack"], 3);
        }

        [Fact]
        public void SetBonus_ShouldGiveNothing_OnASinglePiece()
            => Assert.Empty(AmberStats().SetBonus([Amber(Rarity.Unique, "necklace")]));

        /// <summary>
        /// Родина рахується незалежно від рідкості, а поріг N бере рідкість N-го предмета від найрідкіснішого:
        /// 2 унікальні + 2 звичайні = поріг 2 унікальний (3002) і поріг 4 звичайний (1004).
        /// </summary>
        [Fact]
        public void SetBonus_ShouldTakeEachThresholdsRarity_FromItsNthRarestPiece()
        {
            var bonus = AmberStats().SetBonus(
            [
                Amber(Rarity.Common, "necklace"),
                Amber(Rarity.Unique, "ring"),
                Amber(Rarity.Common, "crown"),
                Amber(Rarity.Unique, "belt")
            ]);

            Assert.Equal(3002 + 1004, bonus["Attack"], 3);
        }

        /// <summary>Унікальний + звичайний: другий за рідкістю — звичайний, тож і поріг 2 звичайний.</summary>
        [Fact]
        public void SetBonus_ShouldGiveTheLowerRarity_WhenTheNthPieceIsLower()
        {
            var bonus = AmberStats().SetBonus([Amber(Rarity.Unique, "necklace"), Amber(Rarity.Common, "ring")]);

            Assert.Equal(1002, bonus["Attack"], 3);
        }

        /// <summary>
        /// Бонус набору важить у силі за вагами, а не сирою сумою: 100 здоров'я × 0.25 = 25, а +10% атаки —
        /// це 10% від усієї пласкої атаки: (100 героя + 4 × 10 предметів) × 0.1 = 14.
        /// </summary>
        [Fact]
        public void Power_ShouldWeighTheSetBonus_LikeTheStatsItGives()
        {
            var config = new GameConfigBuilder().WithHeroes().WithEquipment().Build();
            config.HeroSettings.StatPowerWeights = new Dictionary<string, double> { ["Attack"] = 1.0, ["Defense"] = 1.0, ["Health"] = 0.25 };
            config.Equipment.SetBonuses[0].Stats = new Dictionary<string, double> { ["Health"] = 100, ["AttackPercent"] = 10 };
            var catalog = new GameCatalog(config);
            var stats = new HeroStats(new HeroProgression(config.HeroSettings), catalog);
            var artifacts = new ArtifactStats(catalog);
            var hero = TestKit.Entities.Hero(TestKeys.CommonHero, level: 1);
            var heroConfig = catalog.Hero(TestKeys.CommonHero);
            EquipmentItem[] set =
            [
                SetPiece(TestKeys.Artifact),
                SetPiece(TestKeys.SecondArtifact),
                SetPiece(TestKeys.ThirdArtifact),
                SetPiece(TestKeys.FourthArtifact)
            ];
            var withoutSet = stats.Power(hero, heroConfig) + set.Sum(item => artifacts.Power(item, artifacts.ClassMultiplier(heroConfig.Class)));

            var power = stats.Power(hero, heroConfig, set);

            Assert.Equal(withoutSet + 25 + 14, power, 3);
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
