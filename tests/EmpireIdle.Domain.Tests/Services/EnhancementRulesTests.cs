using EmpireIdle.Domain.Services;
using EmpireIdle.Domain.Services.Config;

namespace EmpireIdle.Domain.Tests.Services
{
    /// <summary>Крива заточки: вартість, шанси, розіграш.</summary>
    public class EnhancementRulesTests
    {
        private static EquipmentConfig Config() => new()
        {
            MaxEnhancement = 20,
            EnhanceBaseGold = 200,
            EnhanceCostGrowth = 1.35,
            SafeEnhancementLevel = 5,
            SuccessDropPerLevel = 0.05,
            MinSuccessChance = 0.25,
            BreakChanceOnFailure = 0.2,
            RepairCostShare = 0.5
        };

        private static EnhancementRules Rules() => new(Config());

        [Fact]
        public void EnhanceCost_ShouldGrowWithLevel()
        {
            var rules = Rules();

            Assert.Equal(200, rules.EnhanceCost(0));
            Assert.True(rules.EnhanceCost(10) > rules.EnhanceCost(9));
        }

        /// <summary>
        /// Ремонт дешевший за спробу, що зламала предмет: поломка забирає
        /// спробу, а не прогрес, і лагодити має бути вигідніше, ніж кидати.
        /// </summary>
        [Fact]
        public void RepairCost_ShouldBeCheaperThanTheAttempt()
        {
            var rules = Rules();

            Assert.True(rules.RepairCost(10) < rules.EnhanceCost(10));
        }

        [Theory]
        [InlineData(0)]
        [InlineData(4)]
        public void SuccessChance_ShouldBeCertain_BelowTheSafeLevel(int level)
            => Assert.Equal(1.0, Rules().SuccessChance(level), 3);

        [Fact]
        public void SuccessChance_ShouldDropWithLevel()
        {
            var rules = Rules();

            Assert.True(rules.SuccessChance(10) < rules.SuccessChance(6));
        }

        [Fact]
        public void SuccessChance_ShouldNeverFallBelowTheFloor()
            => Assert.Equal(0.25, Rules().SuccessChance(19), 3);

        /// <summary>До безпечного рівня зламати предмет неможливо.</summary>
        [Fact]
        public void Roll_ShouldAlwaysSucceed_BelowTheSafeLevel()
        {
            var random = new StubRandom(0.99, 0.99);

            Assert.Equal(EnhancementOutcome.Success, Rules().Roll(0, random));
        }

        [Fact]
        public void Roll_ShouldBreak_WhenBothRollsFail()
        {
            // Перший кидок вище за шанс успіху — невдача; другий нижче
            // за шанс поломки — предмет зламано
            var random = new StubRandom(0.99, 0.01);

            Assert.Equal(EnhancementOutcome.Broken, Rules().Roll(10, random));
        }

        [Fact]
        public void Roll_ShouldOnlyFail_WhenTheBreakRollMisses()
        {
            var random = new StubRandom(0.99, 0.99);

            Assert.Equal(EnhancementOutcome.Failure, Rules().Roll(10, random));
        }

        /// <summary>Заздалегідь задана послідовність кидків.</summary>
        private sealed class StubRandom(params double[] values) : IRandomSource
        {
            private int _index;

            public double NextDouble() => values[Math.Min(_index++, values.Length - 1)];

            public int Next(int maxValue) => 0;

            public int Next(int minValue, int maxValue) => minValue;
        }
    }
}
