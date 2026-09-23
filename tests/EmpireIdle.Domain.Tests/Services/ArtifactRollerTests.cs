using EmpireIdle.Domain.Enums;
using EmpireIdle.Domain.Services;
using EmpireIdle.Domain.Services.Config;
using System.Globalization;

namespace EmpireIdle.Domain.Tests.Services
{
    /// <summary>
    /// Ролли артефактів.
    ///
    /// Головне тут — відтворюваність: у журналі предмета лежить лише сід,
    /// і якщо той самий сід дасть інший набір, журнал стає марним, а на
    /// скаргу «прокачав тричі й усе в сміттєвий стат» відповісти нічим.
    /// </summary>
    public class ArtifactRollerTests
    {
        private static EquipmentConfig Config() => new()
        {
            MaxEnhancement = 20,
            ArtifactBaseStats = 2,
            ArtifactStatLevels = [4, 8],
            ArtifactUpgradeLevels = [12, 16, 20],
            DoubleUpgradeChance = 0.1,
            ArtifactStats =
            [
                new ArtifactStatConfig { Stat = "Attack", Min = 4, Max = 12, UpgradeMin = 1, UpgradeMax = 3 },
                new ArtifactStatConfig { Stat = "Defense", Min = 5, Max = 14, UpgradeMin = 1, UpgradeMax = 4 },
                new ArtifactStatConfig { Stat = "Health", Min = 20, Max = 60, UpgradeMin = 5, UpgradeMax = 15 }
            ],
            ArtifactRarityMultipliers = new Dictionary<string, double>
            {
                ["Common"] = 1.0,
                ["Rare"] = 1.4,
                ["Unique"] = 2.0
            }
        };

        private static ArtifactRoller Roller() => new(Config());

        // ---------- Стартовий набір ----------

        /// <summary>Той самий сід дає той самий результат — інакше журнал марний.</summary>
        [Fact]
        public void RollInitial_ShouldBeReproducible()
        {
            var roller = Roller();

            var first = roller.RollInitial(Rarity.Common, setKey: null, seed: 12345);
            var second = roller.RollInitial(Rarity.Common, setKey: null, seed: 12345);

            Assert.Equal(first, second);
        }

        [Fact]
        public void RollInitial_ShouldGiveExactlyTwoDistinctStats()
        {
            var stats = Roller().RollInitial(Rarity.Common, setKey: null, seed: 7);

            Assert.Equal(2, stats.Count);
            Assert.Equal(2, stats.Keys.Distinct().Count());
        }

        [Fact]
        public void RollInitial_ShouldStayWithinTheBands()
        {
            var config = Config();
            var roller = new ArtifactRoller(config);

            for (var seed = 0; seed < 200; seed++)
            {
                foreach (var (stat, value) in roller.RollInitial(Rarity.Common, setKey: null, seed))
                {
                    var band = config.ArtifactStats.Single(s => s.Stat == stat);

                    Assert.InRange(value, band.Min, band.Max);
                }
            }
        }

        [Fact]
        public void RollInitial_ShouldScaleWithRarity()
        {
            var roller = Roller();

            var common = roller.RollInitial(Rarity.Common, setKey: null, seed: 7).Values.Sum();
            var unique = roller.RollInitial(Rarity.Unique, setKey: null, seed: 7).Values.Sum();

            Assert.True(unique > common, $"unique={unique}, common={common}");
        }

        /// <summary>Різні сіди дають різні набори — інакше ролл не випадковий.</summary>
        [Fact]
        public void RollInitial_ShouldDifferBetweenSeeds()
        {
            var roller = Roller();

            var distinct = Enumerable.Range(0, 50)
                .Select(seed => string.Join(",", roller.RollInitial(Rarity.Common, setKey: null, seed)
                    .OrderBy(s => s.Key)
                    .Select(s => $"{s.Key}:{s.Value}")))
                .Distinct()
                .Count();

            Assert.True(distinct > 10, $"only {distinct} distinct rolls out of 50 seeds");
        }

        // ---------- Нові стати ----------

        [Theory]
        [InlineData(4)]
        [InlineData(8)]
        public void RollForLevel_ShouldAddAStat_OnUnlockLevels(int level)
        {
            var roll = Roller().RollForLevel(level, Rarity.Common, setKey: null, ["Attack"], seed: 3);

            Assert.Single(roll.Added);
            Assert.Empty(roll.Raised);
        }

        [Fact]
        public void RollForLevel_ShouldNotAddAStatTheItemAlreadyHas()
        {
            var roll = Roller().RollForLevel(4, Rarity.Common, setKey: null, ["Attack", "Defense"], seed: 3);

            Assert.DoesNotContain("Attack", roll.Added.Keys);
            Assert.DoesNotContain("Defense", roll.Added.Keys);
        }

        /// <summary>Пул вичерпано — новий стат не дублюється й не падає.</summary>
        [Fact]
        public void RollForLevel_ShouldAddNothing_WhenThePoolIsExhausted()
        {
            var roll = Roller().RollForLevel(4, Rarity.Common, setKey: null, ["Attack", "Defense", "Health"], seed: 3);

            Assert.Empty(roll.Added);
        }

        // ---------- Прокачка наявних ----------

        [Theory]
        [InlineData(12)]
        [InlineData(16)]
        [InlineData(20)]
        public void RollForLevel_ShouldRaiseExistingStats_OnUpgradeLevels(int level)
        {
            var roll = Roller().RollForLevel(level, Rarity.Common, setKey: null, ["Attack", "Defense"], seed: 3);

            Assert.Empty(roll.Added);
            Assert.InRange(roll.Raised.Count, 1, 2);
            Assert.All(roll.Raised.Keys, stat => Assert.Contains(stat, new[] { "Attack", "Defense" }));
        }

        [Fact]
        public void RollForLevel_ShouldStayWithinTheUpgradeBands()
        {
            var config = Config();
            var roller = new ArtifactRoller(config);

            for (var seed = 0; seed < 200; seed++)
            {
                foreach (var (stat, delta) in roller.RollForLevel(12, Rarity.Common, setKey: null, ["Attack", "Defense"], seed).Raised)
                {
                    var band = config.ArtifactStats.Single(s => s.Stat == stat);

                    Assert.InRange(delta, band.UpgradeMin, band.UpgradeMax);
                }
            }
        }

        /// <summary>
        /// Два стати за раз випадають приблизно в одному випадку з десяти.
        /// Перевіряється смугою, а не точним числом: це розподіл, не формула.
        /// </summary>
        [Fact]
        public void RollForLevel_ShouldRaiseTwoStatsAboutOneTimeInTen()
        {
            var roller = Roller();

            var doubles = Enumerable.Range(0, 2000)
                .Count(seed => roller.RollForLevel(12, Rarity.Common, setKey: null, ["Attack", "Defense"], seed).Raised.Count == 2);

            Assert.InRange(doubles / 2000.0, 0.06, 0.15);
        }

        /// <summary>Один наявний стат — качати можна тільки його, і лише раз.</summary>
        [Fact]
        public void RollForLevel_ShouldRaiseASingleStat_WhenThatIsAllThereIs()
        {
            var roller = Roller();

            for (var seed = 0; seed < 100; seed++)
            {
                var roll = roller.RollForLevel(12, Rarity.Common, setKey: null, ["Attack"], seed);

                Assert.Single(roll.Raised);
                Assert.Equal("Attack", roll.Raised.Keys.Single());
            }
        }

        [Fact]
        public void RollForLevel_ShouldChangeNothing_WhenTheItemHasNoStats()
        {
            var roll = Roller().RollForLevel(12, Rarity.Common, setKey: null, [], seed: 3);

            Assert.Empty(roll.Added);
            Assert.Empty(roll.Raised);
        }

        // ---------- Решта рівнів ----------

        /// <summary>
        /// Рівні поза розкладом нічого не дають, але й не падають: прокачка
        /// на них коштує золота й піднімає лише множник заточки.
        /// </summary>
        [Theory]
        [InlineData(1)]
        [InlineData(7)]
        [InlineData(11)]
        [InlineData(19)]
        public void RollForLevel_ShouldChangeNothing_OnPlainLevels(int level)
        {
            var roll = Roller().RollForLevel(level, Rarity.Common, setKey: null, ["Attack"], seed: 3);

            Assert.Empty(roll.Added);
            Assert.Empty(roll.Raised);
        }

        [Fact]
        public void RollForLevel_ShouldBeReproducible()
        {
            var roller = Roller();

            var first = roller.RollForLevel(12, Rarity.Rare, setKey: null, ["Attack", "Defense"], seed: 999);
            var second = roller.RollForLevel(12, Rarity.Rare, setKey: null, ["Attack", "Defense"], seed: 999);

            Assert.Equal(first.Added, second.Added);
            Assert.Equal(first.Raised, second.Raised);
        }

        /// <summary>
        /// У данській «aa» — це «å», і вона сортується після «z». Той самий сід
        /// на хості з іншою культурою не має обирати інший стат.
        /// </summary>
        [Fact]
        public void RollInitial_ShouldPickTheSameStat_RegardlessOfCulture()
        {
            var roller = new ArtifactRoller(new EquipmentConfig
            {
                ArtifactBaseStats = 1,
                ArtifactStats =
                [
                    new ArtifactStatConfig { Stat = "Aarmor", Min = 1, Max = 2, UpgradeMin = 1, UpgradeMax = 1 },
            new ArtifactStatConfig { Stat = "Zeal", Min = 1, Max = 2, UpgradeMin = 1, UpgradeMax = 1 }
                ],
                ArtifactRarityMultipliers = new Dictionary<string, double> { ["Common"] = 1.0 }
            });

            string PickUnder(CultureInfo culture)
            {
                var original = CultureInfo.CurrentCulture;
                CultureInfo.CurrentCulture = culture;

                try
                {
                    return roller.RollInitial(Rarity.Common, setKey: null, seed: 7).Keys.Single();
                }
                finally
                {
                    CultureInfo.CurrentCulture = original;
                }
            }

            Assert.Equal(PickUnder(CultureInfo.InvariantCulture), PickUnder(new CultureInfo("da-DK")));
        }

        // ---------- Рівень і характер набору ----------

        /// <summary>
        /// Дві родини: «ember» рівня 1 без характеру й «obsidian» рівня 3
        /// із захисним характером. Решта конфіга — як у Config().
        /// </summary>
        private static EquipmentConfig ConfigWithSets()
        {
            var config = Config();

            config.ArtifactTierMultipliers = [1.0, 1.5, 2.0];
            config.ArtifactFocusWeight = 3.0;
            config.ArtifactSets =
            [
                new ArtifactSetConfig { Key = "ember", Tier = 1 },
                new ArtifactSetConfig { Key = "obsidian", Tier = 3, FocusStats = ["Defense", "Health"] }
            ];

            return config;
        }

        /// <summary>Набір вищого рівня множить значення — той самий сід, стати ×2.</summary>
        [Fact]
        public void RollInitial_ShouldScaleWithTheSetTier()
        {
            var config = ConfigWithSets();
            config.ArtifactSets[1].FocusStats = []; // лише рівень: вибір статів той самий, що в ember
            var roller = new ArtifactRoller(config);

            var low = roller.RollInitial(Rarity.Common, setKey: "ember_common", seed: 7);
            var high = roller.RollInitial(Rarity.Common, setKey: "obsidian_common", seed: 7);

            Assert.Equal(low.Keys.OrderBy(k => k), high.Keys.OrderBy(k => k));

            foreach (var (stat, value) in low)
                Assert.Equal(Math.Round(value * 2.0, 2), high[stat], precision: 1);
        }

        /// <summary>
        /// Набір без характеру ролить рівно те саме, що й предмет поза родинами:
        /// журнали ролів старих предметів відтворюються тим самим сідом.
        /// </summary>
        [Fact]
        public void RollInitial_ShouldMatchTheUnsetRoll_ForATierOneSetWithoutFocus()
        {
            var roller = new ArtifactRoller(ConfigWithSets());

            for (var seed = 0; seed < 50; seed++)
                Assert.Equal(
                    roller.RollInitial(Rarity.Rare, setKey: null, seed),
                    roller.RollInitial(Rarity.Rare, setKey: "ember_rare", seed));
        }

        /// <summary>
        /// Характер тягне ролл: з вагою 3 у захисного набору атака (єдиний
        /// нехарактерний стат) потрапляє в стартову пару помітно рідше.
        /// </summary>
        [Fact]
        public void RollInitial_ShouldFavourTheFocusStats()
        {
            var roller = new ArtifactRoller(ConfigWithSets());

            var withAttack = Enumerable.Range(0, 2000)
                .Count(seed => roller.RollInitial(Rarity.Common, setKey: "obsidian_common", seed).ContainsKey("Attack"));

            var withAttackUnfocused = Enumerable.Range(0, 2000)
                .Count(seed => roller.RollInitial(Rarity.Common, setKey: null, seed).ContainsKey("Attack"));

            // Без характеру атака в парі з трьох — 2/3 випадків; з вагою 3 — близько 1/3
            Assert.InRange(withAttackUnfocused, 1200, 1470);
            Assert.InRange(withAttack, 500, 800);
        }

        /// <summary>Прокачка бере той самий рівень набору, що й випадіння.</summary>
        [Fact]
        public void RollForLevel_ShouldScaleWithTheSetTier()
        {
            var config = ConfigWithSets();
            config.ArtifactSets[1].FocusStats = [];
            var roller = new ArtifactRoller(config);

            var low = roller.RollForLevel(12, Rarity.Common, "ember_common", ["Attack", "Defense"], seed: 5);
            var high = roller.RollForLevel(12, Rarity.Common, "obsidian_common", ["Attack", "Defense"], seed: 5);

            Assert.Equal(low.Raised.Keys, high.Raised.Keys);

            foreach (var (stat, delta) in low.Raised)
                Assert.Equal(Math.Round(delta * 2.0, 2), high.Raised[stat], precision: 1);
        }

        /// <summary>Невідомий SetKey — не помилка: предмет ролиться без рівня й характеру.</summary>
        [Fact]
        public void RollInitial_ShouldIgnoreAnUnknownSet()
        {
            var roller = new ArtifactRoller(ConfigWithSets());

            Assert.Equal(
                roller.RollInitial(Rarity.Common, setKey: null, seed: 11),
                roller.RollInitial(Rarity.Common, setKey: "nowhere_common", seed: 11));
        }
    }
}
