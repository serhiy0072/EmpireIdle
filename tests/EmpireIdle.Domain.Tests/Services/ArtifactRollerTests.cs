using EmpireIdle.Domain.Entities;
using EmpireIdle.Domain.Enums;
using EmpireIdle.Domain.Services;
using EmpireIdle.Domain.Services.Config;

namespace EmpireIdle.Domain.Tests.Services
{
    /// <summary>
    /// Ранги заточки артефактів (GDD §9.12).
    ///
    /// Головне тут — відтворюваність і коло позицій: у журналі предмета лежить лише сід,
    /// і якщо той самий сід дасть інший ранг, журнал стає марним, а на скаргу «точив тричі —
    /// і все одиниці» відповісти нічим.
    /// </summary>
    public class ArtifactRollerTests
    {
        private static readonly DateTime Now = new(2026, 6, 1, 12, 0, 0, DateTimeKind.Utc);

        private static readonly EquipmentConfig Config = TestKit.GameConfigBuilder.DefaultEquipment("forge");

        private static ArtifactRoller Roller() => new(Config);

        private static EquipmentItem Item()
            => new(Guid.NewGuid(), Guid.NewGuid(), 1, "amulet", EquipmentSlot.Artifact, Rarity.Common, Now);

        /// <summary>Заточує предмет до потрібного рангу, як це робить команда.</summary>
        private static EquipmentItem Sharpened(string slot, int mastery, int seed = 1)
        {
            var item = Item();

            for (var i = 0; i < mastery; i++)
            {
                var rank = Roller().RollRank(slot, item.Mastery, item.Stats, seed + i);
                item.ApplyMasteryRank(rank.Position, rank.Stat, rank.Value, Now);
            }

            return item;
        }

        /// <summary>Той самий сід дає той самий ранг — інакше журнал марний.</summary>
        [Fact]
        public void RollRank_ShouldBeReproducible()
        {
            var item = Sharpened("necklace", mastery: 2);

            var first = Roller().RollRank("necklace", item.Mastery, item.Stats, seed: 12345);
            var second = Roller().RollRank("necklace", item.Mastery, item.Stats, seed: 12345);

            Assert.Equal(first, second);
        }

        /// <summary>Перші два ранги — сталі бонуси слота, у порядку конфігу.</summary>
        [Theory]
        [InlineData("necklace", 0, "Attack")]
        [InlineData("necklace", 1, "UnitAttack")]
        [InlineData("crown", 0, "Defense")]
        [InlineData("belt", 1, "UnitDefense")]
        public void RollRank_ShouldGiveTheFixedBonuses_OnTheFirstTwoRanks(string slot, int mastery, string expected)
        {
            var item = Sharpened(slot, mastery);

            var rank = Roller().RollRank(slot, item.Mastery, item.Stats, seed: 7);

            Assert.Equal((mastery, expected), (rank.Position, rank.Stat));
        }

        /// <summary>Третій і четвертий — із пулу слота й ніколи не однакові.</summary>
        [Fact]
        public void RollRank_ShouldPickTwoDistinctRandomBonuses_FromTheSlotPool()
        {
            var pool = Config.FindArtifactSlot("ring")!.RandomBonuses.Select(e => e.Stat).ToHashSet();

            for (var seed = 0; seed < 300; seed++)
            {
                var item = Sharpened("ring", mastery: 4, seed: seed * 10);
                var third = Assert.Single(item.Stats, s => s.Position == 2).StatKey;
                var fourth = Assert.Single(item.Stats, s => s.Position == 3).StatKey;

                Assert.Contains(third, pool);
                Assert.Contains(fourth, pool);
                Assert.NotEqual(third, fourth);
            }
        }

        /// <summary>Ранги по колу: +5 знову качає перший бонус, +7 — третій, той самий стат.</summary>
        [Theory]
        [InlineData(4, 0)]
        [InlineData(6, 2)]
        [InlineData(19, 3)]
        public void RollRank_ShouldCycleThroughThePositions_KeepingTheirStats(int mastery, int position)
        {
            var item = Sharpened("crown", mastery);
            var held = Assert.Single(item.Stats, s => s.Position == position).StatKey;

            var rank = Roller().RollRank("crown", item.Mastery, item.Stats, seed: 99);

            Assert.Equal((position, held), (rank.Position, rank.Stat));
        }

        /// <summary>Значення рангу — значення ступеня з таблиці свого стату.</summary>
        [Fact]
        public void RollRank_ShouldTakeTheValueFromTheStatsStepTable()
        {
            for (var seed = 0; seed < 50; seed++)
            {
                var rank = Roller().RollRank("necklace", 0, [], seed);

                Assert.Equal(Config.FindArtifactBonus(rank.Stat)!.Steps[rank.Step - 1], rank.Value);
            }
        }

        /// <summary>Ступені 1–4 випадають із шансами 40/30/20/10%.</summary>
        [Fact]
        public void RollRank_ShouldRollStepsWithTheConfiguredChances()
        {
            const int draws = 20_000;

            var counts = Enumerable.Range(0, draws)
                .Select(seed => Roller().RollRank("necklace", 0, [], seed).Step)
                .GroupBy(step => step)
                .ToDictionary(g => g.Key, g => g.Count() / (double)draws);

            Assert.InRange(counts[1], 0.38, 0.42);
            Assert.InRange(counts[2], 0.28, 0.32);
            Assert.InRange(counts[3], 0.18, 0.22);
            Assert.InRange(counts[4], 0.085, 0.115);
        }

        /// <summary>Вага 10 проти 25/30/30: кулдаун третім бонусом корони — близько 10,5%.</summary>
        [Fact]
        public void RollRank_ShouldRespectThePoolWeights()
        {
            const int draws = 20_000;
            var item = Sharpened("crown", mastery: 2);

            var share = Enumerable.Range(0, draws)
                .Count(seed => Roller().RollRank("crown", item.Mastery, item.Stats, seed).Stat == "CooldownReduction")
                / (double)draws;

            Assert.InRange(share, 0.09, 0.12);
        }

        [Fact]
        public void RollRank_ShouldThrow_ForAnUnknownSlot()
            => Assert.Throws<InvalidOperationException>(() => Roller().RollRank("boots", 0, [], seed: 1));
    }
}
