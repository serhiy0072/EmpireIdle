using EmpireIdle.Domain.Services;

namespace EmpireIdle.Domain.Tests.Services
{
    /// <summary>Пропорційний поділ колони на прибутті.</summary>
    public class ReinforcementSplitTests
    {
        [Fact]
        public void Take_ShouldAcceptEverything_WhenTheEmbassyHasRoom()
        {
            var units = new Dictionary<string, int> { ["infantry"] = 10, ["archer"] = 5 };

            var (accepted, rejected) = ReinforcementSplit.Take(units, 20);

            Assert.Equal(15, accepted.Values.Sum());
            Assert.Empty(rejected);
        }

        [Fact]
        public void Take_ShouldRejectEverything_WhenTheEmbassyIsFull()
        {
            var units = new Dictionary<string, int> { ["infantry"] = 10 };

            var (accepted, rejected) = ReinforcementSplit.Take(units, 0);

            Assert.Empty(accepted);
            Assert.Equal(10, rejected["infantry"]);
        }

        /// <summary>Половина слотів — половина кожного типу, нічого не загублено.</summary>
        [Fact]
        public void Take_ShouldSplitProportionally()
        {
            var units = new Dictionary<string, int> { ["infantry"] = 60, ["archer"] = 40 };

            var (accepted, rejected) = ReinforcementSplit.Take(units, 50);

            Assert.Equal(30, accepted["infantry"]);
            Assert.Equal(20, accepted["archer"]);
            Assert.Equal(50, rejected.Values.Sum());
        }

        /// <summary>
        /// Залишок від ділення роздається детерміновано: той самий вхід
        /// у будь-якому порядку ключів дає той самий результат.
        /// </summary>
        [Fact]
        public void Take_ShouldBeDeterministic_WhenRemaindersTie()
        {
            var first = ReinforcementSplit.Take(
                new Dictionary<string, int> { ["archer"] = 3, ["infantry"] = 3 }, 3);

            var second = ReinforcementSplit.Take(
                new Dictionary<string, int> { ["infantry"] = 3, ["archer"] = 3 }, 3);

            Assert.Equal(first.Accepted["archer"], second.Accepted["archer"]);
            Assert.Equal(3, first.Accepted.Values.Sum());
        }
    }
}
