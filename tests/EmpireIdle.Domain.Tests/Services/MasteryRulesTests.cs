using EmpireIdle.Domain.Enums;
using EmpireIdle.Domain.Services;
using EmpireIdle.Domain.Services.Config;

namespace EmpireIdle.Domain.Tests.Services
{
    /// <summary>Заточка коваля: вартість, шанси й поломка за таблицею (GDD §9.12).</summary>
    public class MasteryRulesTests
    {
        private static EquipmentConfig Config() => new()
        {
            MaxMastery = 20,
            MasteryBaseGold = 400,
            MasteryCostGrowth = 1.33,
            MasterySuccessChances = [1, 1, 1, 1, 1, 0.9, 0.85, 0.8, 0.75, 0.7, 0.65, 0.6, 0.55, 0.5, 0.45, 0.4, 0.35, 0.3, 0.27, 0.25],
            MasteryBreakChances = [0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0.1, 0.1, 0.1, 0.1, 0.1, 0.2, 0.2, 0.2, 0.2, 0.2],
            RepairGemsPerMastery = 50,
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
            => Assert.Equal(MasteryOutcome.Success, Rules().Roll(4, new StubRandom(0.999)));

        [Fact]
        public void Roll_ShouldSucceed_WhenTheDrawIsUnderTheChance()
            => Assert.Equal(MasteryOutcome.Success, Rules().Roll(9, new StubRandom(0.69)));

        /// <summary>До +10 невдача лише з'їдає золото: шанс поломки там — нуль.</summary>
        [Fact]
        public void Roll_ShouldFailWithoutBreaking_BeforeTheRiskyRanks()
            => Assert.Equal(MasteryOutcome.Failed, Rules().Roll(9, new StubRandom(0.71, 0.0)));

        /// <summary>Поломка — другий кидок після невдачі: на +16 ламається 20% невдалих спроб.</summary>
        [Theory]
        [InlineData(0.19, MasteryOutcome.Broken)]
        [InlineData(0.21, MasteryOutcome.Failed)]
        public void Roll_ShouldBreak_OnAFailedRiskyRank_ByTheSecondDraw(double breakDraw, MasteryOutcome expected)
            => Assert.Equal(expected, Rules().Roll(15, new StubRandom(0.99, breakDraw)));

        /// <summary>Успіх не ламає ніколи — кидок поломки навіть не робиться.</summary>
        [Fact]
        public void Roll_ShouldNeverBreak_OnSuccess()
            => Assert.Equal(MasteryOutcome.Success, Rules().Roll(15, new StubRandom(0.0, 0.0)));

        [Theory]
        [InlineData(9, 0.0)]
        [InlineData(10, 0.1)]
        [InlineData(15, 0.2)]
        public void BreakChance_ShouldFollowTheTable(int mastery, double expected)
            => Assert.Equal(expected, Rules().BreakChance(mastery), 3);

        [Fact]
        public void BreakChance_ShouldBeZero_WithoutATable()
            => Assert.Equal(0, new MasteryRules(new EquipmentConfig()).BreakChance(19), 3);

        /// <summary>Ремонт — 50 gems за кожен рівень заточки: зламаний на +12 коштує 600.</summary>
        [Fact]
        public void RepairGems_ShouldGrowWithTheMastery()
            => Assert.Equal(600, Rules().RepairGems(12));

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
