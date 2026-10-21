using EmpireIdle.Application.Interfaces;
using EmpireIdle.Domain.Combat;
using EmpireIdle.Domain.Entities;
using EmpireIdle.Domain.Services;

namespace EmpireIdle.Application.Marches.Services
{
    /// <summary>
    /// Герої, що боронять село гравця просто зараз (рішення 15.10.2026): найсильніший кожного класу за
    /// силою зі спорядженням, де б він не був — удома, у марші чи в чужому гарнізоні. Правило вибору —
    /// у домені (GarrisonDefence), тут лише збирання героїв і їхньої сили.
    /// </summary>
    public sealed class GarrisonDefenders
    {
        private readonly IHeroRepository _heroes;
        private readonly IInventoryRepository _inventory;
        private readonly HeroStats _heroStats;
        private readonly GameCatalog _catalog;

        public GarrisonDefenders(IHeroRepository heroes, IInventoryRepository inventory, HeroStats heroStats, GameCatalog catalog)
        {
            _heroes = heroes;
            _inventory = inventory;
            _heroStats = heroStats;
            _catalog = catalog;
        }

        /// <summary>Захисники села гравця; героїв, яких уже немає в довіднику, пропускає.</summary>
        public async Task<IReadOnlyList<Hero>> ForAsync(Guid playerId, CancellationToken cancellationToken)
        {
            var candidates = new List<(Hero Hero, string Class, double Power)>();
            var heroes = await _heroes.GetByPlayerAsync(playerId, cancellationToken);

            // Спорядження всього ростеру одним запитом: героїв у гравця два десятки
            var equipped = (await _inventory.GetEquippedByHeroesAsync(heroes.Select(h => h.Id).ToList(), cancellationToken))
                .ToLookup(e => e.EquippedByHeroId);

            foreach (var hero in heroes)
            {
                if (_catalog.FindHero(hero.HeroKey) is not { } config)
                    continue;

                candidates.Add((hero, config.Class, _heroStats.Power(hero, config, equipped[hero.Id].ToList())));
            }

            return GarrisonDefence.Pick(candidates, _catalog.Config.HeroSettings.GarrisonDefenders);
        }
    }
}
