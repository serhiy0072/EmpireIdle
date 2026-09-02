using EmpireIdle.Domain.Services;

namespace EmpireIdle.Domain.Tests.Services
{
    public class CombatCalculatorTests
    {
        private readonly CombatCalculator _calculator;
        private readonly Dictionary<string, int> _attacker = new() { ["infantry"] = 20 };
        private readonly Dictionary<string, int> _defender = new() { ["infantry"] = 5 };

        public CombatCalculatorTests()
        {
            var combatConfig = new CombatConfig
            {
                RandomSigma = 0.15,
                RandomMin = 0.7,
                RandomMax = 1.4
            };

            var catalog = new GameCatalog(new GameConfig
            {
                Units =
                [
                    new()
                    {
                        Key = "infantry",
                        Stats = new Dictionary<string, double> { ["Attack"] = 10, ["Defense"] = 12 }
                    }
                ],
                Buildings = [new BuildingConfig { Key = "townhall", IsMainBuilding = true }]
            });

            _calculator = new CombatCalculator(combatConfig, catalog);
        }

        [Fact]
        public void Resolve_ShouldBeReproducibleForTheSameSeed()
        {
            var first = _calculator.Resolve(_attacker, _defender, "plain", seed: 42);
            var second = _calculator.Resolve(_attacker, _defender, "plain", seed: 42);

            Assert.Equal(first.AttackerPower, second.AttackerPower);
            Assert.Equal(first.AttackerWon, second.AttackerWon);
        }

        [Fact]
        public void CalculatePower_ShouldIgnoreRandomness()
        {
            var power = _calculator.CalculatePower(_attacker, "plain", isAttacker: true);

            // 20 піхотинців × 10 атаки, без терейн-бонусу
            Assert.Equal(200, power);
        }
    }
}
