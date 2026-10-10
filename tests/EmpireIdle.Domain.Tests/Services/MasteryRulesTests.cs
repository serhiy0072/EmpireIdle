using EmpireIdle.Domain.Services;
using EmpireIdle.Domain.Services.Config;

namespace EmpireIdle.Domain.Tests.Services
{
    /// <summary>Заточка коваля: вартість і шанси за таблицею (GDD §9.12).</summary>
    public class MasteryRulesTests
    {
        private static EquipmentConfig Config() => new()
        {
            MaxMastery = 20,
            MasteryBaseGold = 400,
            MasteryCostGrowth = 1.33,
            MasterySuccessChances = [1, 1, 1, 1, 1, 0.9, 0.85, 0.8, 0.75, 0.7, 0.65, 0.6, 0.55, 0.5, 0.45, 0.4, 0.35, 0.3, 0.27, 0.25],
        };

        private static MasteryRules Rules() => new(Config());

        /// <summary>round10(400 · 1.33^поточна): 400, 530, 710, 940 … 90 210 за спробу на +20.</summary>
        [Theory]
        [InlineData(0, 400)]
        [InlineData(1, 530)]
        [InlineData(2, 710)]
        [InlineData(3, 940)]
        [InlineData(19, 90_210)]
        public void Cost_ShouldGrowGeometrically_RoundedToTens(int mastery, int expected)
            => Assert.Equal(expected, Rules().Cost(mastery));

        [Theory]
        [InlineData(0, 1.0)]
        [InlineData(4, 1.0)]
        [InlineData(5, 0.9)]
        [InlineData(19, 0.25)]
        public void SuccessChance_ShouldFollowTheTable(int mastery, double expected)
            => Assert.Equal(expected, Rules().SuccessChance(mastery), 3);

        [Fact]
        public void SuccessChance_ShouldKeepTheLastEntry_BeyondTheTable()
            => Assert.Equal(0.25, Rules().SuccessChance(25), 3);

        [Fact]
        public void Roll_ShouldAlwaysSucceed_WhileTheChanceIsCertain()
            => Assert.True(Rules().Roll(4, new StubRandom(0.999)));

        [Fact]
        public void Roll_ShouldSucceed_WhenTheDrawIsUnderTheChance()
            => Assert.True(Rules().Roll(9, new StubRandom(0.69)));

        [Fact]
        public void Roll_ShouldFail_WhenTheDrawIsOverTheChance()
            => Assert.False(Rules().Roll(9, new StubRandom(0.71)));

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
