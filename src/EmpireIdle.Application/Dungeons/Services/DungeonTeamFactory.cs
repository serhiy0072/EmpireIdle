using EmpireIdle.Application.Interfaces;
using EmpireIdle.Domain.Dungeons;
using EmpireIdle.Domain.Entities;
using EmpireIdle.Domain.Enums;
using EmpireIdle.Domain.Exceptions;
using EmpireIdle.Domain.Services;

namespace EmpireIdle.Application.Dungeons.Services
{
    /// <summary>
    /// Збирає бойовий склад героїв: стати з рівня, тіру й спорядження плюс
    /// унікальні властивості артефактів. Окремо від рушія, бо рушій не має
    /// ходити в інвентар, і окремо від команди, бо збір потрібен і перегляду.
    /// </summary>
    public class DungeonTeamFactory
    {
        private readonly IHeroRepository _heroRepository;
        private readonly IInventoryRepository _inventoryRepository;
        private readonly GameCatalog _catalog;
        private readonly HeroStats _heroStats;
        private readonly HeroProgression _progression;

        public DungeonTeamFactory(
            IHeroRepository heroRepository,
            IInventoryRepository inventoryRepository,
            GameCatalog catalog,
            HeroStats heroStats,
            HeroProgression progression)
        {
            _heroRepository = heroRepository;
            _inventoryRepository = inventoryRepository;
            _catalog = catalog;
            _heroStats = heroStats;
            _progression = progression;
        }

        /// <summary>
        /// Бойові стати команди в порядку, заданому гравцем.
        /// Чужий, поранений чи невідомий герой у склад не потрапляє —
        /// це відмова, а не тихе викидання зі списку.
        /// </summary>
        public async Task<List<DungeonHero>> BuildAsync(Guid playerId, IReadOnlyList<Guid> heroIds, CancellationToken cancellationToken)
        {
            var team = new List<DungeonHero>();

            foreach (var heroId in heroIds)
            {
                var hero = await _heroRepository.GetByIdAsync(heroId, cancellationToken)
                    ?? throw new EntityNotFoundException("Hero", heroId.ToString());

                if (hero.PlayerId != playerId)
                    throw new EntityNotFoundException("Hero", heroId.ToString());

                var config = _catalog.FindHero(hero.HeroKey)
                    ?? throw new EntityNotFoundException("Hero config", hero.HeroKey);

                if (hero.State == HeroState.OnMarket)
                    throw new RequirementNotMetException(RefusalReasons.HeroOnMarket, $"Hero {hero.Id} is on the market.");

                // Поранити героя могли між вибором складу й стартом — наприклад, у бою маршу
                if (hero.State == HeroState.Wounded)
                    throw new RequirementNotMetException(RefusalReasons.DungeonHeroWounded,
                        $"Hero {hero.Id} is wounded and cannot enter a dungeon.", config.DisplayName);

                var equipped = await _inventoryRepository.GetEquippedAsync(hero.Id, cancellationToken);
                var stats = _heroStats.Compute(hero, config, equipped);

                team.Add(new DungeonHero(
                    hero.Id,
                    hero.HeroKey,
                    config.Class,
                    stats.GetValueOrDefault("Attack"),
                    stats.GetValueOrDefault("Defense"),
                    stats.GetValueOrDefault("Health"),
                    // Швидкість героя задає чергу ходів; без власної береться типова
                    _progression.MarchSpeed(config),
                    UniqueStats(equipped)));
            }

            return team;
        }

        /// <summary>
        /// Унікальні властивості з надітих артефактів. Однакові складаються:
        /// два предмети на крит мають давати більше, ніж один — інакше набір
        /// із чотирьох частин не мав би сенсу.
        /// </summary>
        private Dictionary<DungeonStat, double> UniqueStats(IReadOnlyCollection<EquipmentItem> equipped)
        {
            var result = new Dictionary<DungeonStat, double>();

            foreach (var item in equipped.Where(i => !i.IsBroken))
            {
                var config = _catalog.FindItem(item.ItemKey);

                if (config?.UniqueStat is not { } stat)
                    continue;

                result[stat] = result.GetValueOrDefault(stat) + config.UniqueStatValue;
            }

            return result;
        }
    }
}
