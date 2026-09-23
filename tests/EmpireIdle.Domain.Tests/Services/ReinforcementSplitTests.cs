using EmpireIdle.Domain.Services;
using EmpireIdle.Domain.ValueObjects;

namespace EmpireIdle.Domain.Tests.Services
{
    /// <summary>Пропорційний поділ колони на прибутті.</summary>
    public class ReinforcementSplitTests
    {
        [Fact]
        public void Take_ShouldAcceptEverything_WhenTheEmbassyHasRoom()
        {
            var units = new Dictionary<UnitStackKey, int> { [new UnitStackKey("infantry", 1)] = 10, [new UnitStackKey("archer", 1)] = 5 };

            var (accepted, rejected) = ReinforcementSplit.Take(units, 20);

            Assert.Equal(15, accepted.Values.Sum());
            Assert.Empty(rejected);
        }

        [Fact]
        public void Take_ShouldRejectEverything_WhenTheEmbassyIsFull()
        {
            var units = new Dictionary<UnitStackKey, int> { [new UnitStackKey("infantry", 1)] = 10 };

            var (accepted, rejected) = ReinforcementSplit.Take(units, 0);

            Assert.Empty(accepted);
            Assert.Equal(10, rejected[new UnitStackKey("infantry", 1)]);
        }

        /// <summary>Половина слотів — половина кожного типу, нічого не загублено.</summary>
        [Fact]
        public void Take_ShouldSplitProportionally()
        {
            var units = new Dictionary<UnitStackKey, int> { [new UnitStackKey("infantry", 1)] = 60, [new UnitStackKey("archer", 1)] = 40 };

            var (accepted, rejected) = ReinforcementSplit.Take(units, 50);

            Assert.Equal(30, accepted[new UnitStackKey("infantry", 1)]);
            Assert.Equal(20, accepted[new UnitStackKey("archer", 1)]);
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
                new Dictionary<UnitStackKey, int> { [new UnitStackKey("archer", 1)] = 3, [new UnitStackKey("infantry", 1)] = 3 }, 3);

            var second = ReinforcementSplit.Take(
                new Dictionary<UnitStackKey, int> { [new UnitStackKey("infantry", 1)] = 3, [new UnitStackKey("archer", 1)] = 3 }, 3);

            Assert.Equal(first.Accepted[new UnitStackKey("archer", 1)], second.Accepted[new UnitStackKey("archer", 1)]);
            Assert.Equal(3, first.Accepted.Values.Sum());
        }
    }
}
