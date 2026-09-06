using EmpireIdle.Domain.Services;
using EmpireIdle.Domain.Services.Config;

namespace EmpireIdle.Domain.Tests.Services
{
    public class BattleResolverTests
    {
        private readonly BattleResolver _resolver;
        private readonly Dictionary<string, int> _attacker = new() { ["infantry"] = 20 };
        private readonly Dictionary<string, int> _defender = new() { ["infantry"] = 15 };

        public BattleResolverTests()
        {
            var combatConfig = new CombatConfig
            {
                RandomSigma = 0.15,
                RandomMin = 0.7,
                RandomMax = 1.4,
                WoundedShareMin = 0.3,
                WoundedShareMax = 0.5,
                RecoverableShare = 0.2
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

            _resolver = new BattleResolver(
                new CombatCalculator(combatConfig, catalog),
                new CasualtySplitter(combatConfig));
        }

        /// <summary>
        /// Бій відтворюється ЦІЛКОМ за сідом — і переможець, і склад втрат.
        ///
        /// До виділення резолвера розподіл втрат брав Random.Shared, тому
        /// переграний бій давав того самого переможця, але інших поранених,
        /// і на скаргу «загинуло більше, ніж мало» відповісти було нічим.
        /// </summary>
        [Fact]
        public void Resolve_ShouldBeFullyReproducible_ForTheSameSeed()
        {
            var first = _resolver.Resolve(_attacker, _defender, "plain", seed: 99,
                attackerBonus: 1.0, defenderBonus: 1.0, woundedCapacity: 50);

            var second = _resolver.Resolve(_attacker, _defender, "plain", seed: 99,
                attackerBonus: 1.0, defenderBonus: 1.0, woundedCapacity: 50);

            Assert.Equal(first.Battle.AttackerWon, second.Battle.AttackerWon);
            Assert.Equal(first.Battle.AttackerPower, second.Battle.AttackerPower);
            Assert.Equal(first.AttackerCasualties.Wounded, second.AttackerCasualties.Wounded);
            Assert.Equal(first.AttackerCasualties.Recoverable, second.AttackerCasualties.Recoverable);
            Assert.Equal(first.AttackerCasualties.Dead, second.AttackerCasualties.Dead);
        }

        /// <summary>Розкладка втрат покриває рівно ті втрати, що дав бій.</summary>
        [Fact]
        public void Resolve_ShouldSplitExactlyTheBattleLosses()
        {
            var outcome = _resolver.Resolve(_attacker, _defender, "plain", seed: 5,
                attackerBonus: 1.0, defenderBonus: 1.0, woundedCapacity: 50);

            foreach (var (unitType, lost) in outcome.Battle.AttackerLosses)
            {
                var accounted = outcome.AttackerCasualties.Wounded.GetValueOrDefault(unitType)
                                + outcome.AttackerCasualties.Recoverable.GetValueOrDefault(unitType)
                                + outcome.AttackerCasualties.Dead.GetValueOrDefault(unitType);

                Assert.Equal(lost, accounted);
            }
        }

        /// <summary>Бонус захисника змінює результат — інакше стіни нічого не варті.</summary>
        [Fact]
        public void Resolve_ShouldAccountForTheDefenderBonus()
        {
            var neutral = _resolver.Resolve(_attacker, _defender, "plain", seed: 11,
                attackerBonus: 1.0, defenderBonus: 1.0, woundedCapacity: 50);

            var fortified = _resolver.Resolve(_attacker, _defender, "plain", seed: 11,
                attackerBonus: 1.0, defenderBonus: 2.0, woundedCapacity: 50);

            Assert.True(fortified.Battle.DefenderPower > neutral.Battle.DefenderPower);
        }
    }
}
