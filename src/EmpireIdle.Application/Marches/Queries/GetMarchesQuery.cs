using EmpireIdle.Application.Common.Security;
using EmpireIdle.Application.Interfaces;
using EmpireIdle.Application.Marches.ReadModels;
using EmpireIdle.Domain.Entities;
using EmpireIdle.Domain.Enums;
using EmpireIdle.Domain.Services;
using MediatR;

namespace EmpireIdle.Application.Marches.Queries
{
    /// <summary>Активні походи гравця: у дорозі до цілі або додому.</summary>
    public record GetMarchesQuery(Guid PlayerId) : IRequest<List<MarchView>>, IPlayerScopedRequest;

    /// <summary>
    /// Обробник GetMarchesQuery. Назву цілі бере легким читанням за id —
    /// повний MarchTargetResolver будує армію оборони, для списку це зайве.
    /// </summary>
    public sealed class GetMarchesQueryHandler : IRequestHandler<GetMarchesQuery, List<MarchView>>
    {
        private readonly IVillageRepository _villageRepository;
        private readonly IGarrisonRepository _garrisonRepository;
        private readonly IMarchRepository _marchRepository;
        private readonly IMonsterRepository _monsterRepository;
        private readonly IHeroRepository _heroRepository;
        private readonly HeroStats _heroStats;
        private readonly GameCatalog _catalog;
        private readonly TimeProvider _timeProvider;
        private readonly SpeedUpCalculator _calculator;

        public GetMarchesQueryHandler(
            IVillageRepository villageRepository,
            IGarrisonRepository garrisonRepository,
            IMarchRepository marchRepository,
            IMonsterRepository monsterRepository,
            IHeroRepository heroRepository,
            HeroStats heroStats,
            GameCatalog catalog,
            TimeProvider timeProvider,
            SpeedUpCalculator calculator)
        {
            _villageRepository = villageRepository;
            _garrisonRepository = garrisonRepository;
            _marchRepository = marchRepository;
            _monsterRepository = monsterRepository;
            _heroRepository = heroRepository;
            _heroStats = heroStats;
            _catalog = catalog;
            _timeProvider = timeProvider;
            _calculator = calculator;
        }

        public async Task<List<MarchView>> Handle(GetMarchesQuery request, CancellationToken cancellationToken)
        {
            var now = _timeProvider.GetUtcNow().UtcDateTime;

            var village = await _villageRepository.GetByPlayerIdReadOnlyAsync(request.PlayerId, cancellationToken)
                ?? throw new InvalidOperationException($"Village not found for player {request.PlayerId}.");

            var garrison = await _garrisonRepository.GetByVillageIdReadOnlyAsync(village.Id, cancellationToken)
                ?? throw new InvalidOperationException($"Garrison not found for village {village.Id}.");

            var marches = await _marchRepository.GetActiveByGarrisonAsync(garrison.Id, cancellationToken);
            var views = new List<MarchView>(marches.Count);

            // Цілі — пакетом за типом, а не запитом на кожен марш
            var monsters = (await _monsterRepository.GetByIdsAsync(
                    TargetIds(marches, MarchTargetType.Monster), cancellationToken))
                .ToDictionary(m => m.Id);
            var villageNames = await _villageRepository.GetNamesAsync(
                TargetIds(marches, MarchTargetType.Village), cancellationToken);
            var heroes = await _heroRepository.GetByMarchesReadOnlyAsync(
                marches.Select(m => m.Id).ToList(), cancellationToken);

            // Найближче прибуття — першим: за ним гравець і стежить
            foreach (var march in marches.OrderBy(m => m.ArrivesAt))
            {
                var (targetName, targetLevel) = ResolveTarget(march, monsters, villageNames);

                views.Add(new MarchView(
                    march.Id,
                    march.TargetType,
                    march.TargetId,
                    targetName,
                    targetLevel,
                    march.TargetX,
                    march.TargetY,
                    march.Intent,
                    march.State,
                    _heroStats.StrongestFirst(heroes[march.Id]).Select(h => h.Id).ToList(),
                    march.DepartedAt,
                    march.LegStartedAt,
                    march.ArrivesAt,
                    march.Units.Select(u => new MarchUnitView(u.UnitType, u.Level, u.Count)).ToList(),
                    _calculator.GetCost(march.ArrivesAt, now)));
            }

            return views;
        }

        private static List<Guid> TargetIds(IEnumerable<March> marches, MarchTargetType type)
            => marches.Where(m => m.TargetType == type).Select(m => m.TargetId).Distinct().ToList();

        /// <summary>
        /// Назва цілі як у прев'ю бою й рівень монстра окремо; null — ціль уже
        /// зникла з мапи. У села рівня немає.
        /// </summary>
        private (string? Name, int? Level) ResolveTarget(March march, IReadOnlyDictionary<Guid, Monster> monsters,
            IReadOnlyDictionary<Guid, string> villageNames)
            => march.TargetType switch
            {
                MarchTargetType.Monster => monsters.TryGetValue(march.TargetId, out var monster)
                    ? (_catalog.MonsterName(monster.Type), monster.Level)
                    : (null, null),
                MarchTargetType.Village => (villageNames.GetValueOrDefault(march.TargetId), null),
                _ => (null, null)
            };
    }
}
