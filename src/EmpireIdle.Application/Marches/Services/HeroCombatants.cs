using EmpireIdle.Application.Interfaces;
using EmpireIdle.Domain.Combat;
using EmpireIdle.Domain.Entities;
using EmpireIdle.Domain.Enums;
using EmpireIdle.Domain.Services;

namespace EmpireIdle.Application.Marches.Services
{
    /// <summary>
    /// Герої як бійці армійського бою (варіант B, рішення 08.10.2026): повні стати героя —
    /// рівень, тір, зірки, зброя й спорядження — дають його силу й здоров'я в бою.
    /// Поранений герой не б'ється: його вже поклав попередній бій.
    /// </summary>
    public sealed class HeroCombatants
    {
        private readonly IInventoryRepository _inventory;
        private readonly HeroStats _heroStats;
        private readonly GameCatalog _catalog;

        public HeroCombatants(IInventoryRepository inventory, HeroStats heroStats, GameCatalog catalog)
        {
            _inventory = inventory;
            _heroStats = heroStats;
            _catalog = catalog;
        }

        /// <param name="attacking">true — сила героя з атаки, false — із захисту.</param>
        public async Task<IReadOnlyList<HeroCombatant>> ForAsync(IEnumerable<Hero?> heroes, bool attacking,
            CancellationToken cancellationToken)
        {
            var combatants = new List<HeroCombatant>();

            foreach (var hero in heroes)
            {
                if (hero is null || hero.State == HeroState.Wounded || _catalog.FindHero(hero.HeroKey) is not { } config)
                    continue;

                var equipped = await _inventory.GetEquippedAsync(hero.Id, cancellationToken);
                var stats = _heroStats.Compute(hero, config, equipped);

                combatants.Add(new HeroCombatant(
                    hero.Id,
                    stats.GetValueOrDefault(attacking ? "Attack" : "Defense"),
                    stats.GetValueOrDefault("Health")));
            }

            return combatants;
        }
    }
}
