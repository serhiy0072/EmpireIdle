using EmpireIdle.Domain.Services;
using EmpireIdle.Domain.Services.Config;

namespace EmpireIdle.Domain.Tests.Services
{
    public class CasualtySplitterTests
    {
        private static CasualtySplitter Splitter() => new(new CombatConfig
        {
            WoundedShareMin = 0.3,
            WoundedShareMax = 0.5,
            RecoverableShare = 0.2
        });

        private static readonly Dictionary<string, int> Losses = new()
        {
            ["infantry"] = 100,
            ["archer"] = 40
        };

        /// <summary>
        /// Той самий сід дає ту саму розкладку. Це і є причина, чому Split
        /// приймає сід замість Random.Shared: сід зберігається у звіті,
        /// і бій має відтворюватись цілком, а не лише переможцем.
        /// </summary>
        [Fact]
        public void Split_ShouldBeReproducible_ForTheSameSeed()
        {
            var splitter = Splitter();

            var first = splitter.Split(Losses, woundedCapacity: 100, seed: 42);
            var second = splitter.Split(Losses, woundedCapacity: 100, seed: 42);

            Assert.Equal(first.Wounded, second.Wounded);
            Assert.Equal(first.Recoverable, second.Recoverable);
            Assert.Equal(first.Dead, second.Dead);
        }

        /// <summary>
        /// Жоден юніт не губиться й не подвоюється: сума трьох кошиків
        /// дорівнює втратам. Найважливіший інваріант — гравець рахує
        /// армію до й після бою.
        /// </summary>
        [Theory]
        [InlineData(0)]
        [InlineData(30)]
        [InlineData(1000)]
        public void Split_ShouldPreserveTotalLosses(int capacity)
        {
            var result = Splitter().Split(Losses, capacity, seed: 7);

            foreach (var (unitType, lost) in Losses)
            {
                var accounted = result.Wounded.GetValueOrDefault(unitType)
                                + result.Recoverable.GetValueOrDefault(unitType)
                                + result.Dead.GetValueOrDefault(unitType);

                Assert.Equal(lost, accounted);
            }
        }

        /// <summary>Без Госпіталю поранених немає — усі втрати безповоротні або відновлювані.</summary>
        [Fact]
        public void Split_ShouldAdmitNobody_WhenHospitalIsFull()
        {
            var result = Splitter().Split(Losses, woundedCapacity: 0, seed: 7);

            Assert.Empty(result.Wounded);
        }

        /// <summary>
        /// Місткість спільна на всі типи: перший тип забирає її, наступним
        /// лишається менше. Інакше госпіталь лікував би N юнітів кожного типу.
        /// </summary>
        [Fact]
        public void Split_ShouldShareHospitalCapacityAcrossUnitTypes()
        {
            var result = Splitter().Split(Losses, woundedCapacity: 20, seed: 7);

            Assert.True(result.Wounded.Values.Sum() <= 20);
        }

        /// <summary>Нульові втрати не потрапляють у кошики.</summary>
        [Fact]
        public void Split_ShouldIgnoreZeroLosses()
        {
            var losses = new Dictionary<string, int> { ["infantry"] = 0 };

            var result = Splitter().Split(losses, woundedCapacity: 100, seed: 7);

            Assert.Empty(result.Wounded);
            Assert.Empty(result.Recoverable);
            Assert.Empty(result.Dead);
        }
    }
}
