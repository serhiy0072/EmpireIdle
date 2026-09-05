using EmpireIdle.Domain.Enums;
using EmpireIdle.Domain.Services;
using EmpireIdle.Domain.Services.Config;

namespace EmpireIdle.Domain.Tests.Services
{
    /// <summary>
    /// Смуга шансів — те, що бачить гравець замість відсотка. Пороги в конфізі,
    /// тому тести перевіряють саме розкладку по смугах, а не конкретні числа.
    /// </summary>
    public class BattleOddsTests
    {
        private static CombatCalculator Calculator() => new(
            new CombatConfig { PreviewOddsThresholds = [2.5, 1.3, 0.8, 0.4] },
            new GameCatalog(new GameConfig
            {
                Buildings = [new BuildingConfig { Key = "townhall", IsMainBuilding = true }],
                Units =
                [
                    new UnitConfig
                    {
                        Key = "infantry",
                        Stats = new Dictionary<string, double> { ["Attack"] = 10, ["Defense"] = 10 }
                    }
                ]
            }));

        [Theory]
        [InlineData(300, 100, BattleOdds.Overwhelming)]  // ×3.0
        [InlineData(250, 100, BattleOdds.Overwhelming)]  // рівно на порозі
        [InlineData(200, 100, BattleOdds.Favoured)]      // ×2.0
        [InlineData(100, 100, BattleOdds.Even)]          // ×1.0
        [InlineData(80, 100, BattleOdds.Even)]           // рівно на порозі
        [InlineData(50, 100, BattleOdds.Risky)]          // ×0.5
        [InlineData(10, 100, BattleOdds.Hopeless)]       // ×0.1
        public void EstimateOdds_ShouldMapRatioToBand(double attacker, double defender, BattleOdds expected)
        {
            Assert.Equal(expected, Calculator().EstimateOdds(attacker, defender));
        }

        /// <summary>
        /// Захисник без сили — не ділення на нуль, а очевидна перемога.
        /// Порожня клітина чи гарнізон без юнітів трапляються постійно.
        /// </summary>
        [Fact]
        public void EstimateOdds_ShouldReturnOverwhelming_AgainstAnEmptyDefence()
        {
            Assert.Equal(BattleOdds.Overwhelming, Calculator().EstimateOdds(100, 0));
        }

        /// <summary>Порожня атака проти будь-якої оборони — безнадія, не виняток.</summary>
        [Fact]
        public void EstimateOdds_ShouldReturnHopeless_WithoutAnArmy()
        {
            Assert.Equal(BattleOdds.Hopeless, Calculator().EstimateOdds(0, 100));
        }

        /// <summary>
        /// Кількість смуг задається конфігом: три пороги дають чотири смуги,
        /// і код не має знати, скільки їх.
        /// </summary>
        [Fact]
        public void EstimateOdds_ShouldFollowTheConfiguredBandCount()
        {
            var calculator = new CombatCalculator(
                new CombatConfig { PreviewOddsThresholds = [2.0, 1.0] },
                new GameCatalog(new GameConfig
                {
                    Buildings = [new BuildingConfig { Key = "townhall", IsMainBuilding = true }]
                }));

            Assert.Equal((BattleOdds)0, calculator.EstimateOdds(300, 100));
            Assert.Equal((BattleOdds)1, calculator.EstimateOdds(150, 100));
            Assert.Equal((BattleOdds)2, calculator.EstimateOdds(50, 100));
        }

        /// <summary>
        /// Прев'ю рахує ту саму CalculatePower, що й бій: інакше воно
        /// розійшлося б із результатом на першому ж ребалансі статів.
        /// </summary>
        [Fact]
        public void EstimateOdds_ShouldUseTheSamePowerFormulaAsTheBattle()
        {
            var calculator = Calculator();
            var attacker = new Dictionary<string, int> { ["infantry"] = 20 };
            var defender = new Dictionary<string, int> { ["infantry"] = 10 };

            var attackerPower = calculator.CalculatePower(attacker, "plain", isAttacker: true);
            var defenderPower = calculator.CalculatePower(defender, "plain", isAttacker: false);

            // 200 проти 100 = ×2.0 → Favoured
            Assert.Equal(BattleOdds.Favoured, calculator.EstimateOdds(attackerPower, defenderPower));
        }
    }
}
