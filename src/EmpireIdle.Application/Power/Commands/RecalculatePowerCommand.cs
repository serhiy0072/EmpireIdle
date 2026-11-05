using EmpireIdle.Application.Interfaces;
using EmpireIdle.Domain.Combat;
using EmpireIdle.Domain.Entities;
using EmpireIdle.Domain.Enums;
using EmpireIdle.Domain.Services;
using EmpireIdle.Domain.Technologies;
using EmpireIdle.Domain.ValueObjects;
using MediatR;
using Microsoft.Extensions.Logging;

namespace EmpireIdle.Application.Power.Commands
{
    /// <summary>
    /// Перераховує бойову силу гравця, якому належить гарнізон.
    ///
    /// Гарнізон, а не гравець, бо всі події, що змінюють армію, несуть саме
    /// його: тренування, бій, повернення походу.
    /// </summary>
    public record RecalculatePowerCommand(Guid GarrisonId) : IRequest;

    public sealed class RecalculatePowerCommandHandler : IRequestHandler<RecalculatePowerCommand>
    {
        /// <summary>
        /// Місцевість для рейтингової сили. Нейтральна навмисно: Power порівнює
        /// гравців між собою, і бонус за терейн зробив би число залежним від
        /// того, де стоїть село, а не від того, яка в гравця армія.
        /// Прев'ю бою бере справжню місцевість окремо.
        /// </summary>
        private const string NeutralTerrain = "plain";

        private readonly IGarrisonRepository _garrisonRepository;
        private readonly IVillageRepository _villageRepository;
        private readonly IMarchRepository _marchRepository;
        private readonly IPlayerPowerRepository _powerRepository;
        private readonly IHeroRepository _heroRepository;
        private readonly IInventoryRepository _inventoryRepository;
        private readonly IPlayerResearchRepository _playerResearch;
        private readonly IClanRepository _clans;
        private readonly IClanResearchRepository _clanResearch;
        private readonly PersonalTechnologies _personalTechnologies;
        private readonly ClanTechnologies _clanTechnologies;
        private readonly IUnitOfWork _unitOfWork;
        private readonly CombatCalculator _combat;
        private readonly TimeProvider _timeProvider;
        private readonly HeroStats _heroStats;
        private readonly GameCatalog _catalog;
        private readonly ILogger<RecalculatePowerCommandHandler> _logger;

        public RecalculatePowerCommandHandler(
            IGarrisonRepository garrisonRepository,
            IVillageRepository villageRepository,
            IMarchRepository marchRepository,
            IPlayerPowerRepository powerRepository,
            IHeroRepository heroRepository,
            IInventoryRepository inventoryRepository,
            IPlayerResearchRepository playerResearch,
            IClanRepository clans,
            IClanResearchRepository clanResearch,
            PersonalTechnologies personalTechnologies,
            ClanTechnologies clanTechnologies,
            IUnitOfWork unitOfWork,
            CombatCalculator combat,
            TimeProvider timeProvider,
            HeroStats heroStats,
            GameCatalog catalog,
            ILogger<RecalculatePowerCommandHandler> logger)
        {
            _garrisonRepository = garrisonRepository;
            _villageRepository = villageRepository;
            _marchRepository = marchRepository;
            _powerRepository = powerRepository;
            _heroRepository = heroRepository;
            _inventoryRepository = inventoryRepository;
            _playerResearch = playerResearch;
            _clans = clans;
            _clanResearch = clanResearch;
            _personalTechnologies = personalTechnologies;
            _clanTechnologies = clanTechnologies;
            _unitOfWork = unitOfWork;
            _combat = combat;
            _timeProvider = timeProvider;
            _heroStats = heroStats;
            _catalog = catalog;
            _logger = logger;
        }

        public async Task Handle(RecalculatePowerCommand request, CancellationToken cancellationToken)
        {
            var now = _timeProvider.GetUtcNow().UtcDateTime;

            var garrison = await _garrisonRepository.GetByIdAsync(request.GarrisonId, cancellationToken);
            if (garrison is null)
                return;

            var village = await _villageRepository.GetByIdAsync(garrison.VillageId, cancellationToken);
            if (village is null)
                return;

            // Гарнізон плюс армії в поході. Марші рахуються обов'язково —
            // інакше Power падає під час атаки, і гравці тримали б військо
            // вдома заради рейтингу
            var army = garrison.Units.ToDictionary(u => new UnitStackKey(u.UnitType, u.Level), u => u.Count);

            var marches = await _marchRepository.GetActiveByGarrisonAsync(garrison.Id, cancellationToken);

            foreach (var march in marches)
            {
                foreach (var (stack, count) in march.GetUnits())
                    army[stack] = army.GetValueOrDefault(stack) + count;
            }

            // Підкріплення в чужих селах рахуються власнику (§7.1): інакше
            // допомога клану коштувала б рейтингу, і її перестали б надсилати.
            // Рівень тут не зберігається (денормалізований підрахунок за типом) —
            // рахуємо як рівень 1, це трохи занижує Power для прокачаних підкріплень.
            var deployed = await _garrisonRepository.GetDeployedReinforcementsAsync(
                village.PlayerId, cancellationToken);

            foreach (var (unitType, count) in deployed)
            {
                var stack = new UnitStackKey(unitType, 1);
                army[stack] = army.GetValueOrDefault(stack) + count;
            }

            // Поранені й відновлювані не входять: вони не б'ються
            var armyPower = _combat.CalculatePower(army, NeutralTerrain, isAttacker: true);

            // Герої рахуються всі, де б не стояли: ростер належить гравцеві,
            // а не гарнізону. Поранені теж — на піку своєї сили, бо вони
            // не гинуть і повертаються в строй, щойно їх вилікують
            var heroes = await _heroRepository.GetByPlayerAsync(village.PlayerId, cancellationToken);

            var heroPower = 0.0;
            var equipmentPower = 0.0;

            // Спорядження всього ростера — одним запитом: команда йде після кожного тренування
            // й бою, і запит на кожного героя множив би їх на розмір ростера
            var equippedByHero = (await _inventoryRepository.GetEquippedByHeroesAsync(
                    heroes.Select(h => h.Id).ToList(), cancellationToken))
                .ToLookup(e => e.EquippedByHeroId!.Value);

            foreach (var hero in heroes)
            {
                var heroConfig = _catalog.FindHero(hero.HeroKey);

                if (heroConfig is null)
                    continue;

                var equipped = equippedByHero[hero.Id].ToList();

                var bare = _heroStats.Power(hero, heroConfig);

                var full = _heroStats.Power(hero, heroConfig, equipped);

                heroPower += bare;

                // Спорядження окремою колонкою: гравець має бачити, скільки
                // сили дає ростер, а скільки — те, що на ньому вдягнене
                equipmentPower += full - bare;
            }

            var power = await _powerRepository.GetByPlayerAsync(village.PlayerId, cancellationToken);

            if (power is null)
            {
                power = new PlayerPower(Guid.NewGuid(), village.PlayerId, village.ServerId, now);
                await _powerRepository.AddAsync(power, cancellationToken);
            }

            var weights = _catalog.Config.Technologies.PowerWeights;

            var research = await _playerResearch.GetByPlayerAsync(village.PlayerId, cancellationToken);
            var technologyPower = research is null ? 0 : _personalTechnologies.Tree.Power(research.Levels, weights);

            // Кланова частина — лише поки гравець у клані: без клану вона зникає разом із бонусами
            var clanId = await _clans.GetClanIdByMemberAsync(village.PlayerId, cancellationToken);
            var clanResearch = clanId is { } id ? await _clanResearch.GetByClanAsync(id, cancellationToken) : null;
            var clanTechnologyPower = clanResearch is null ? 0 : _clanTechnologies.Tree.Power(clanResearch.Levels, weights);

            power.Set(armyPower, heroPower, equipmentPower, technologyPower, clanTechnologyPower, now);

            await _unitOfWork.SaveChangesAsync(cancellationToken);

            _logger.LogDebug("Power recalculated for player {PlayerId}: {Total}", village.PlayerId, power.TotalPower);
        }
    }
}
