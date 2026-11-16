using EmpireIdle.Application.Interfaces;
using EmpireIdle.Application.Research.Services;
using EmpireIdle.Domain.Combat;
using EmpireIdle.Domain.Entities;
using EmpireIdle.Domain.Enums;
using EmpireIdle.Domain.Services;
using EmpireIdle.Domain.Services.Config;
using EmpireIdle.Domain.Technologies;

namespace EmpireIdle.Application.Marches.Services
{
    /// <summary>
    /// Герої як бійці армійського бою (варіант B, рішення 08.10.2026): повні стати героя —
    /// рівень, тір, зірки, зброя й спорядження — дають його силу й здоров'я в бою.
    /// Поранений герой не б'ється: його вже поклав попередній бій.
    /// Бойові технології власника множать силу героя так само, як у вежі й данжі (рішення 03.11.2026).
    /// </summary>
    public sealed class HeroCombatants
    {
        private readonly IInventoryRepository _inventory;
        private readonly HeroStats _heroStats;
        private readonly GameCatalog _catalog;
        private readonly PlayerTechnologies _technologies;

        public HeroCombatants(IInventoryRepository inventory, HeroStats heroStats, GameCatalog catalog,
            PlayerTechnologies technologies)
        {
            _technologies = technologies;
            _inventory = inventory;
            _heroStats = heroStats;
            _catalog = catalog;
        }

        /// <param name="attacking">true — сила героя з атаки, false — із захисту.</param>
        public async Task<IReadOnlyList<HeroCombatant>> ForAsync(IEnumerable<Hero?> heroes, bool attacking,
            CancellationToken cancellationToken)
        {
            var combatants = new List<HeroCombatant>();
            var fighting = Fighting(heroes);
            var gear = await EquippedAsync(fighting, cancellationToken);
            var technologies = await TechnologiesAsync(fighting, cancellationToken);

            foreach (var (hero, config) in fighting)
            {
                var stats = _heroStats.Compute(hero, config, gear[hero.Id].ToList());
                var troops = stats.GetValueOrDefault(attacking ? ArtifactStats.UnitAttack : ArtifactStats.UnitDefense);

                // Оберіг у відсотках, як і решта статів; стеля та сама, що й у данжі
                var ward = Math.Min(_catalog.Config.Dungeons.MaxWard, stats.GetValueOrDefault("Ward") / 100);

                // Крит — це удар: множить атаку, а захист лишає як є
                var power = attacking
                    ? stats.GetValueOrDefault("Attack") * HeroCrit.DamageMultiplier(stats, _catalog.Config.Dungeons)
                    : stats.GetValueOrDefault("Defense");

                // Герой бере бойові технології юнітів своєї ролі: «для всіх» плюс бонус його типу; без ролі — лише «для всіх»
                var unitType = _catalog.Config.HeroSettings.RoleUnits.GetValueOrDefault(config.Class);
                power *= 1 + technologies[hero.PlayerId].Of(attacking ? TechnologyStat.Attack : TechnologyStat.Defense, unitType);

                combatants.Add(new HeroCombatant(
                    hero.Id,
                    power,
                    stats.GetValueOrDefault("Health"),
                    troops * _catalog.Config.Equipment.UnitStatPower,
                    ward));
            }

            return combatants;
        }

        /// <summary>
        /// Підсумкові стати героїв зі спорядженням — для бонусів військам (HeroCombatModifiers).
        /// Поранених пропускає: бонус від них однаково гасне.
        /// </summary>
        public async Task<HeroGear> GearAsync(IEnumerable<Hero?> heroes, CancellationToken cancellationToken)
        {
            var fighting = Fighting(heroes);
            var gear = await EquippedAsync(fighting, cancellationToken);

            var stats = fighting.ToDictionary(f => f.Hero.Id,
                f => _heroStats.Compute(f.Hero, f.Config, gear[f.Hero.Id].ToList()));

            return stats.Count == 0 ? HeroGear.None : new HeroGear(stats);
        }

        /// <summary>Хто б'ється: живий, не поранений, з відомим конфігом; кожен герой один раз.</summary>
        private List<(Hero Hero, HeroConfig Config)> Fighting(IEnumerable<Hero?> heroes)
            => heroes
                .OfType<Hero>()
                .Where(h => h.State != HeroState.Wounded)
                .DistinctBy(h => h.Id)
                .Select(h => (Hero: h, Config: _catalog.FindHero(h.HeroKey)))
                .Where(f => f.Config is not null)
                .Select(f => (f.Hero, f.Config!))
                .ToList();

        /// <summary>Технології власників: в обороні села поруч стоять герої різних гравців, тож по запиту на власника, а не на героя.</summary>
        private async Task<Dictionary<Guid, TechnologyBonuses>> TechnologiesAsync(List<(Hero Hero, HeroConfig Config)> fighting,
            CancellationToken cancellationToken)
        {
            var byOwner = new Dictionary<Guid, TechnologyBonuses>();

            foreach (var owner in fighting.Select(f => f.Hero.PlayerId).Distinct())
                byOwner[owner] = await _technologies.ForPlayerAsync(owner, cancellationToken);

            return byOwner;
        }

        /// <summary>Спорядження всіх бійців одним запитом: бій за село з гарнізоном інакше робив би запит на кожного.</summary>
        private async Task<ILookup<Guid, EquipmentItem>> EquippedAsync(List<(Hero Hero, HeroConfig Config)> fighting,
            CancellationToken cancellationToken)
        {
            if (fighting.Count == 0)
                return Array.Empty<EquipmentItem>().ToLookup(e => Guid.Empty);

            var items = await _inventory.GetEquippedByHeroesAsync(fighting.Select(f => f.Hero.Id).ToList(), cancellationToken);

            return items.ToLookup(e => e.EquippedByHeroId!.Value);
        }
    }
}
