using EmpireIdle.Domain.Combat;
using EmpireIdle.Domain.Entities;
using EmpireIdle.Domain.Services;
using EmpireIdle.Domain.Services.Config;
using EmpireIdle.Domain.ValueObjects;

namespace EmpireIdle.Domain.Tests.Services
{
    public class CombatCalculatorTests
    {
        private readonly CombatCalculator _calculator;
        private readonly Dictionary<UnitStackKey, int> _attacker = new() { [new UnitStackKey("infantry", 1)] = 20 };
        private readonly Dictionary<UnitStackKey, int> _defender = new() { [new UnitStackKey("infantry", 1)] = 5 };
        private static Dictionary<UnitStackKey, int> Army(int count) => new() { [new UnitStackKey("infantry", 1)] = count };

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
            var first = _calculator.Resolve(_attacker, DefenceStacks.FromArmy(_defender), "plain", seed: 42);
            var second = _calculator.Resolve(_attacker, DefenceStacks.FromArmy(_defender), "plain", seed: 42);

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

        /// <summary>
        /// Рівень юніта підсилює його внесок у силу: +10%/рівень (§5.2 GDD).
        /// Без цього прокачка була б косметикою, а не бойовою перевагою.
        /// </summary>
        [Fact]
        public void CalculatePower_ShouldApplyTheLevelMultiplier()
        {
            var levelledArmy = new Dictionary<UnitStackKey, int> { [new UnitStackKey("infantry", 3)] = 20 };

            var power = _calculator.CalculatePower(levelledArmy, "plain", isAttacker: true);

            // 20 × 10 атаки × (1 + 0.10 × (3-1)) = 240
            Assert.Equal(240, power, 3);
        }

        /// <summary>Той самий множник рівня діє й на обороні.</summary>
        [Fact]
        public void CalculateDefencePower_ShouldApplyTheLevelMultiplier()
        {
            var plain = new List<DefenceStack> { new(null, "infantry", 1, 100) };
            var levelled = new List<DefenceStack> { new(null, "infantry", 3, 100) };

            var plainPower = _calculator.CalculateDefencePower(plain, "plain");
            var levelledPower = _calculator.CalculateDefencePower(levelled, "plain");

            Assert.Equal(plainPower * 1.20, levelledPower, 3);
        }

        /// <summary>
        /// Каталог із двома типами різної стійкості: облога втричі
        /// крихкіша за піхоту, атака однакова — щоб різниця у втратах
        /// пояснювалась саме захистом, а не внеском у силу.
        /// </summary>
        private static CombatCalculator CalculatorWithFragileSiege()
        {
            var combatConfig = new CombatConfig
            {
                RandomSigma = 0.15,
                RandomMin = 0.7,
                RandomMax = 1.4,
                NoLossShareThreshold = 0.03
            };

            var catalog = new GameCatalog(new GameConfig
            {
                Units =
                [
                    new()
                    {
                        Key = "infantry",
                        Stats = new Dictionary<string, double> { ["Attack"] = 10, ["Defense"] = 12 }
                    },
                    new()
                    {
                        Key = "siege",
                        Stats = new Dictionary<string, double> { ["Attack"] = 10, ["Defense"] = 4 }
                    }
                ],
                Buildings = [new BuildingConfig { Key = "townhall", IsMainBuilding = true }]
            });

            return new CombatCalculator(combatConfig, catalog);
        }

        /// <summary>Частка втраченого від початкового складу.</summary>
        private static double Share(IReadOnlyDictionary<UnitStackKey, int> army, IReadOnlyDictionary<UnitStackKey, int> losses)
            => (double)losses.Values.Sum() / army.Values.Sum();

        [Fact]
        public void Resolve_ShouldHitFragileUnitsHarder_WhenSideWins()
        {
            // Arrange: змішана армія проти явно слабшого захисника
            var calculator = CalculatorWithFragileSiege();

            var attacker = new Dictionary<UnitStackKey, int> { [new UnitStackKey("infantry", 1)] = 100, [new UnitStackKey("siege", 1)] = 100 };
            var defender = new Dictionary<UnitStackKey, int> { [new UnitStackKey("infantry", 1)] = 10 };

            // Act
            var result = calculator.Resolve(attacker, DefenceStacks.FromArmy(defender), "plain", seed: 42);

            // Assert
            Assert.True(result.AttackerWon);

            var infantryShare = result.AttackerLosses[new UnitStackKey("infantry", 1)] / 100.0;
            var siegeShare = result.AttackerLosses[new UnitStackKey("siege", 1)] / 100.0;

            // Захист 4 проти 12 — облога має танути помітно швидше
            Assert.True(siegeShare > infantryShare,
                $"siege {siegeShare:P1} має бути більшим за infantry {infantryShare:P1}");
        }

        [Fact]
        public void Resolve_ShouldSpreadLossesEvenly_WhenSideLoses()
        {
            // Arrange: змішана армія, яка гарантовано програє
            var calculator = CalculatorWithFragileSiege();

            var attacker = new Dictionary<UnitStackKey, int> { [new UnitStackKey("infantry", 1)] = 100, [new UnitStackKey("siege", 1)] = 100 };
            var defender = new Dictionary<UnitStackKey, int> { [new UnitStackKey("infantry", 1)] = 1000 };

            // Act
            var result = calculator.Resolve(attacker, DefenceStacks.FromArmy(defender), "plain", seed: 42);

            // Assert
            Assert.False(result.AttackerWon);

            // Розгром не розбирає якість: однакова частка на обидва типи,
            // різниця можлива хіба на одиницю залишку при непарних втратах
            Assert.True(Math.Abs(result.AttackerLosses[new UnitStackKey("infantry", 1)] - result.AttackerLosses[new UnitStackKey("siege", 1)]) <= 1,
                $"infantry {result.AttackerLosses[new UnitStackKey("infantry", 1)]} проти siege {result.AttackerLosses[new UnitStackKey("siege", 1)]}");
        }

        // ---------- Смуги втрат ----------

        /// <summary>
        /// Переможений більше не втрачає все. До смуг це був би повний нуль,
        /// і одна програна оборона викреслювала б гравця з гри на тижні.
        /// </summary>
        [Fact]
        public void Resolve_ShouldNeverWipeTheLoser()
        {
            var attacker = Army(2000);
            var defender = Army(100);

            var result = _calculator.Resolve(attacker, DefenceStacks.FromArmy(defender), "plain", seed: 7);

            Assert.True(result.AttackerWon);
            Assert.True(Share(defender, result.DefenderLosses) < 0.55);
        }

        /// <summary>
        /// Головний інваріант смуг: за будь-якого результату переможений
        /// втрачає більшу частку, ніж переможець. Інакше оптимальною
        /// стратегією стало б навмисно програвати.
        /// </summary>
        [Theory]
        [InlineData(1)]
        [InlineData(42)]
        [InlineData(1337)]
        [InlineData(90210)]
        public void Resolve_ShouldCostTheLoserMoreThanTheWinner(int seed)
        {
            var attacker = Army(1000);
            var defender = Army(900);

            var result = _calculator.Resolve(attacker, DefenceStacks.FromArmy(defender), "plain", seed);

            var attackerShare = Share(attacker, result.AttackerLosses);
            var defenderShare = Share(defender, result.DefenderLosses);

            if (result.AttackerWon)
                Assert.True(defenderShare > attackerShare,
                    $"loser {defenderShare:P1} vs winner {attackerShare:P1}");
            else
                Assert.True(attackerShare > defenderShare,
                    $"loser {attackerShare:P1} vs winner {defenderShare:P1}");
        }

        /// <summary>Розгромлена оборона лишає селу половину війська.</summary>
        [Fact]
        public void Resolve_ShouldLeaveHalfTheArmy_WhenDefenceIsCrushed()
        {
            var attacker = Army(2000);
            var defender = Army(100);

            var result = _calculator.Resolve(attacker, DefenceStacks.FromArmy(defender), "plain", seed: 11);

            Assert.True(result.AttackerWon);

            // Стеля смуги 0.50, допуск на округлення вгору
            Assert.True(Share(defender, result.DefenderLosses) <= 0.51);
        }

        /// <summary>
        /// Провалена атака дорожча за провалену оборону: нападник сам
        /// обрав бій, захисник — ні.
        /// </summary>
        [Fact]
        public void Resolve_ShouldPunishAFailedAttackHarderThanAFailedDefence()
        {
            var weakAttacker = Army(100);
            var strongDefender = Army(2000);
            var failedAttack = _calculator.Resolve(weakAttacker, DefenceStacks.FromArmy(strongDefender), "plain", seed: 5);

            var strongAttacker = Army(2000);
            var weakDefender = Army(100);
            var failedDefence = _calculator.Resolve(strongAttacker, DefenceStacks.FromArmy(weakDefender), "plain", seed: 5);

            Assert.False(failedAttack.AttackerWon);
            Assert.True(failedDefence.AttackerWon);

            Assert.True(
                Share(weakAttacker, failedAttack.AttackerLosses)
                > Share(weakDefender, failedDefence.DefenderLosses));
        }

        /// <summary>Розгромна перемога лишається в межах своєї смуги.</summary>
        [Fact]
        public void Resolve_ShouldKeepWinnerLossesWithinTheBand()
        {
            var attacker = Army(2000);
            var defender = Army(100);

            var result = _calculator.Resolve(attacker, DefenceStacks.FromArmy(defender), "plain", seed: 3);

            Assert.True(result.AttackerWon);
            Assert.True(Share(attacker, result.AttackerLosses) <= 0.36);
        }

        // ---------- Пасивки в обороні ----------

        /// <summary>Пасивка лідера піднімає силу оборони, не чіпаючи складу.</summary>
        [Fact]
        public void CalculateDefencePower_ShouldApplyTheOwnersBuff()
        {
            var stacks = new List<DefenceStack> { new(null, "infantry", 1, 100) };

            var plain = _calculator.CalculateDefencePower(stacks, "plain");
            var buffed = _calculator.CalculateDefencePower(stacks, "plain",
                new DefenceBuffs(TestKit.Passives.Buff(passives: TestKit.Passives.Defence(10)), new()));

            Assert.Equal(plain * 1.10, buffed, 3);
        }

        /// <summary>
        /// Бонус союзника не тече на юнітів господаря. Головна перевірка
        /// коміту: один лідер на гарнізон підсилював би чужі стеки.
        /// </summary>
        [Fact]
        public void CalculateDefencePower_ShouldNotLeakBetweenOwners()
        {
            var ally = Guid.NewGuid();

            var stacks = new List<DefenceStack>
            {
                new(null, "infantry", 1, 100),
                new(ally, "infantry", 1, 100)
            };

            var buffs = new DefenceBuffs(
                StackBuff.None,
                new Dictionary<Guid, StackBuff> { [ally] = TestKit.Passives.Buff(passives: TestKit.Passives.Defence(20)) });

            var actual = _calculator.CalculateDefencePower(stacks, "plain", buffs);
            var plain = _calculator.CalculateDefencePower(stacks, "plain");

            // Підсилилась рівно половина складу
            Assert.Equal(plain * 1.10, actual, 3);
        }

        [Fact]
        public void CalculateDefencePower_ShouldMatchThePlainSum_WhenThereAreNoBuffs()
        {
            var army = new Dictionary<UnitStackKey, int> { [new UnitStackKey("infantry", 1)] = 50, [new UnitStackKey("archer", 1)] = 20 };

            Assert.Equal(
                _calculator.CalculatePower(army, "plain", isAttacker: false),
                _calculator.CalculateDefencePower(DefenceStacks.FromArmy(army), "plain"),
                3);
        }
    }
}
