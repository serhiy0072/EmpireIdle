using EmpireIdle.Domain.Entities;
using EmpireIdle.Domain.Services;
using EmpireIdle.Domain.Services.Config;

namespace EmpireIdle.Domain.Tests.Services
{
    public class CombatCalculatorTests
    {
        private readonly CombatCalculator _calculator;
        private readonly Dictionary<string, int> _attacker = new() { ["infantry"] = 20 };
        private readonly Dictionary<string, int> _defender = new() { ["infantry"] = 5 };
        private static Dictionary<string, int> Army(int count) => new() { ["infantry"] = count };

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
        private static double Share(IReadOnlyDictionary<string, int> army, IReadOnlyDictionary<string, int> losses)
            => (double)losses.Values.Sum() / army.Values.Sum();

        [Fact]
        public void Resolve_ShouldHitFragileUnitsHarder_WhenSideWins()
        {
            // Arrange: змішана армія проти явно слабшого захисника
            var calculator = CalculatorWithFragileSiege();

            var attacker = new Dictionary<string, int> { ["infantry"] = 100, ["siege"] = 100 };
            var defender = new Dictionary<string, int> { ["infantry"] = 10 };

            // Act
            var result = calculator.Resolve(attacker, DefenceStacks.FromArmy(defender), "plain", seed: 42);

            // Assert
            Assert.True(result.AttackerWon);

            var infantryShare = result.AttackerLosses["infantry"] / 100.0;
            var siegeShare = result.AttackerLosses["siege"] / 100.0;

            // Захист 4 проти 12 — облога має танути помітно швидше
            Assert.True(siegeShare > infantryShare,
                $"siege {siegeShare:P1} має бути більшим за infantry {infantryShare:P1}");
        }

        [Fact]
        public void Resolve_ShouldSpreadLossesEvenly_WhenSideLoses()
        {
            // Arrange: змішана армія, яка гарантовано програє
            var calculator = CalculatorWithFragileSiege();

            var attacker = new Dictionary<string, int> { ["infantry"] = 100, ["siege"] = 100 };
            var defender = new Dictionary<string, int> { ["infantry"] = 1000 };

            // Act
            var result = calculator.Resolve(attacker, DefenceStacks.FromArmy(defender), "plain", seed: 42);

            // Assert
            Assert.False(result.AttackerWon);

            // Розгром не розбирає якість: однакова частка на обидва типи,
            // різниця можлива хіба на одиницю залишку при непарних втратах
            Assert.True(Math.Abs(result.AttackerLosses["infantry"] - result.AttackerLosses["siege"]) <= 1,
                $"infantry {result.AttackerLosses["infantry"]} проти siege {result.AttackerLosses["siege"]}");
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

        /// <summary>
        /// Бонус будується так само, як у бою: через пасивку з конфіга.
        /// Складати StackBuff вручну тест не може й не має — тоді він
        /// перевіряв би власну арифметику, а не шлях від JSON до формули.
        /// </summary>
        private static StackBuff Buff(double defensePercent, string target = "infantry")
        {
            var config = new GameConfig
            {
                Buildings =
                [
                    new BuildingConfig { Key = "townhall", IsMainBuilding = true },
                    new BuildingConfig { Key = "heroeshall" },
                    new BuildingConfig { Key = "hospital" }
                ],
                Resources = [new ResourceConfig { Key = "food" }],
                Units = [new UnitConfig { Key = "infantry" }, new UnitConfig { Key = "archer" }],
                HeroSettings = new HeroesConfig
                {
                    MaxTier = 1,
                    LevelsPerTier = 10,
                    MaxMarches = 3,
                    BuildingKey = "heroeshall",
                    HealBuildingKey = "hospital",
                    HealCostPerLevel = [new ResourceCost { Resource = "food", Amount = 40 }],
                    Classes = ["warrior"],
                    TierStatMultipliers = [1.0],
                    EvolutionItemKeys = [],
                    OverflowGems = new Dictionary<string, int> { ["Common"] = 0 }
                },
                Heroes =
                [
                    new HeroConfig
                    {
                        Key = "buffer",
                        Class = "warrior",
                        SummonShards = 10,
                        ShardPriceGold = 100,
                        LevelUpCosts =
                        [
                            new HeroLevelCostBand
                            {
                                FromLevel = 1,
                                Cost = [new ResourceCost { Resource = "food", Amount = 50 }]
                            }
                        ],
                        Passives =
                        [
                            new HeroPassiveConfig
                            {
                                Key = "guard", Target = target, Stat = "Defense",
                                UnlockConstellation = 0, BasePercent = defensePercent
                            }
                        ]
                    }
                ]
            };

            var hero = new Hero(Guid.NewGuid(), Guid.NewGuid(), 1, "buffer", Guid.NewGuid(),
                asLeader: true, new DateTime(2026, 6, 1, 12, 0, 0, DateTimeKind.Utc));

            return new HeroCombatModifiers(new GameCatalog(config)).For(hero);
        }

        /// <summary>Пасивка лідера піднімає силу оборони, не чіпаючи складу.</summary>
        [Fact]
        public void CalculateDefencePower_ShouldApplyTheOwnersBuff()
        {
            var stacks = new List<DefenceStack> { new(null, "infantry", 100) };

            var plain = _calculator.CalculateDefencePower(stacks, "plain");
            var buffed = _calculator.CalculateDefencePower(stacks, "plain",
                new DefenceBuffs(Buff(defensePercent: 10), new()));

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
                new(null, "infantry", 100),
                new(ally, "infantry", 100)
            };

            var buffs = new DefenceBuffs(
                StackBuff.None,
                new Dictionary<Guid, StackBuff> { [ally] = Buff(defensePercent: 20) });

            var actual = _calculator.CalculateDefencePower(stacks, "plain", buffs);
            var plain = _calculator.CalculateDefencePower(stacks, "plain");

            // Підсилилась рівно половина складу
            Assert.Equal(plain * 1.10, actual, 3);
        }

        [Fact]
        public void CalculateDefencePower_ShouldMatchThePlainSum_WhenThereAreNoBuffs()
        {
            var army = new Dictionary<string, int> { ["infantry"] = 50, ["archer"] = 20 };

            Assert.Equal(
                _calculator.CalculatePower(army, "plain", isAttacker: false),
                _calculator.CalculateDefencePower(DefenceStacks.FromArmy(army), "plain"),
                3);
        }
    }
}
