using EmpireIdle.Domain.Services;
using EmpireIdle.Domain.Services.Config;

namespace EmpireIdle.Domain.Tests.Services
{
    /// <summary>Майстерність коваля: вартість, шанси, розіграш без поломки.</summary>
    public class MasteryRulesTests
    {
        private static EquipmentConfig Config() => new()
        {
            MaxMastery = 10,
            MasteryBaseGold = 500,
            MasteryCostGrowth = 1.7,
            SafeMasteryLevel = 3,
            SuccessDropPerLevel = 0.1,
            MinSuccessChance = 0.3,
        };

        private static MasteryRules Rules() => new(Config());

        [Fact]
        public void Cost_ShouldStartAtTheBase_AndGrowGeometrically()
        {
            var rules = Rules();

            Assert.Equal(500, rules.Cost(0));
            Assert.Equal(850, rules.Cost(1));
            Assert.True(rules.Cost(9) > rules.Cost(8));
        }

        [Theory]
        [InlineData(0)]
        [InlineData(2)]
        public void SuccessChance_ShouldBeCertain_BelowTheSafeLevel(int mastery)
            => Assert.Equal(1.0, Rules().SuccessChance(mastery), 3);

        /// <summary>Від безпечного рівня шанс спадає на крок за рівень: 3 → 90%, 5 → 70%.</summary>
        [Theory]
        [InlineData(3, 0.9)]
        [InlineData(5, 0.7)]
        public void SuccessChance_ShouldDropLinearly_FromTheSafeLevel(int mastery, double expected)
            => Assert.Equal(expected, Rules().SuccessChance(mastery), 3);

        [Fact]
        public void SuccessChance_ShouldNeverFallBelowTheFloor()
            => Assert.Equal(0.3, Rules().SuccessChance(9), 3);

        [Fact]
        public void Roll_ShouldAlwaysSucceed_BelowTheSafeLevel()
            => Assert.True(Rules().Roll(0, new StubRandom(0.99)));

        [Fact]
        public void Roll_ShouldSucceed_WhenTheDrawIsUnderTheChance()
            => Assert.True(Rules().Roll(5, new StubRandom(0.69)));

        [Fact]
        public void Roll_ShouldFail_WhenTheDrawIsOverTheChance()
            => Assert.False(Rules().Roll(5, new StubRandom(0.71)));

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
