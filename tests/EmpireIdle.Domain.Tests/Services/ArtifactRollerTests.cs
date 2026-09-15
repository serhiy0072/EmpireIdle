using EmpireIdle.Domain.Enums;
using EmpireIdle.Domain.Services;
using EmpireIdle.Domain.Services.Config;

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

            var first = roller.RollInitial(Rarity.Common, seed: 12345);
            var second = roller.RollInitial(Rarity.Common, seed: 12345);

            Assert.Equal(first, second);
        }

        [Fact]
        public void RollInitial_ShouldGiveExactlyTwoDistinctStats()
        {
            var stats = Roller().RollInitial(Rarity.Common, seed: 7);

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
                foreach (var (stat, value) in roller.RollInitial(Rarity.Common, seed))
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

            var common = roller.RollInitial(Rarity.Common, seed: 7).Values.Sum();
            var unique = roller.RollInitial(Rarity.Unique, seed: 7).Values.Sum();

            Assert.True(unique > common, $"unique={unique}, common={common}");
        }

        /// <summary>Різні сіди дають різні набори — інакше ролл не випадковий.</summary>
        [Fact]
        public void RollInitial_ShouldDifferBetweenSeeds()
        {
            var roller = Roller();

            var distinct = Enumerable.Range(0, 50)
                .Select(seed => string.Join(",", roller.RollInitial(Rarity.Common, seed)
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
            var roll = Roller().RollForLevel(level, Rarity.Common, ["Attack"], seed: 3);

            Assert.Single(roll.Added);
            Assert.Empty(roll.Raised);
        }

        [Fact]
        public void RollForLevel_ShouldNotAddAStatTheItemAlreadyHas()
        {
            var roll = Roller().RollForLevel(4, Rarity.Common, ["Attack", "Defense"], seed: 3);

            Assert.DoesNotContain("Attack", roll.Added.Keys);
            Assert.DoesNotContain("Defense", roll.Added.Keys);
        }

        /// <summary>Пул вичерпано — новий стат не дублюється й не падає.</summary>
        [Fact]
        public void RollForLevel_ShouldAddNothing_WhenThePoolIsExhausted()
        {
            var roll = Roller().RollForLevel(4, Rarity.Common, ["Attack", "Defense", "Health"], seed: 3);

            Assert.Empty(roll.Added);
        }

        // ---------- Прокачка наявних ----------

        [Theory]
        [InlineData(12)]
        [InlineData(16)]
        [InlineData(20)]
        public void RollForLevel_ShouldRaiseExistingStats_OnUpgradeLevels(int level)
        {
            var roll = Roller().RollForLevel(level, Rarity.Common, ["Attack", "Defense"], seed: 3);

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
                foreach (var (stat, delta) in roller.RollForLevel(12, Rarity.Common, ["Attack", "Defense"], seed).Raised)
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
                .Count(seed => roller.RollForLevel(12, Rarity.Common, ["Attack", "Defense"], seed).Raised.Count == 2);

            Assert.InRange(doubles / 2000.0, 0.06, 0.15);
        }

        /// <summary>Один наявний стат — качати можна тільки його, і лише раз.</summary>
        [Fact]
        public void RollForLevel_ShouldRaiseASingleStat_WhenThatIsAllThereIs()
        {
            var roller = Roller();

            for (var seed = 0; seed < 100; seed++)
            {
                var roll = roller.RollForLevel(12, Rarity.Common, ["Attack"], seed);

                Assert.Single(roll.Raised);
                Assert.Equal("Attack", roll.Raised.Keys.Single());
            }
        }

        [Fact]
        public void RollForLevel_ShouldChangeNothing_WhenTheItemHasNoStats()
        {
            var roll = Roller().RollForLevel(12, Rarity.Common, [], seed: 3);

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
            var roll = Roller().RollForLevel(level, Rarity.Common, ["Attack"], seed: 3);

            Assert.Empty(roll.Added);
            Assert.Empty(roll.Raised);
        }

        [Fact]
        public void RollForLevel_ShouldBeReproducible()
        {
            var roller = Roller();

            var first = roller.RollForLevel(12, Rarity.Rare, ["Attack", "Defense"], seed: 999);
            var second = roller.RollForLevel(12, Rarity.Rare, ["Attack", "Defense"], seed: 999);

            Assert.Equal(first.Added, second.Added);
            Assert.Equal(first.Raised, second.Raised);
        }
    }
}
