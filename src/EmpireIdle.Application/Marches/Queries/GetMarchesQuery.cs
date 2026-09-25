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
        private readonly GameCatalog _catalog;
        private readonly TimeProvider _timeProvider;
        private readonly SpeedUpCalculator _calculator;

        public GetMarchesQueryHandler(
            IVillageRepository villageRepository,
            IGarrisonRepository garrisonRepository,
            IMarchRepository marchRepository,
            IMonsterRepository monsterRepository,
            GameCatalog catalog,
            TimeProvider timeProvider,
            SpeedUpCalculator calculator)
        {
            _villageRepository = villageRepository;
            _garrisonRepository = garrisonRepository;
            _marchRepository = marchRepository;
            _monsterRepository = monsterRepository;
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

            // Найближче прибуття — першим: за ним гравець і стежить
            foreach (var march in marches.OrderBy(m => m.ArrivesAt))
            {
                var (targetName, targetLevel) = await ResolveTargetAsync(march, cancellationToken);

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
                    march.HeroId,
                    march.DepartedAt,
                    march.LegStartedAt,
                    march.ArrivesAt,
                    march.Units.Select(u => new MarchUnitView(u.UnitType, u.Level, u.Count)).ToList(),
                    _calculator.GetCost(march.ArrivesAt, now)));
            }

            return views;
        }

        /// <summary>
        /// Назва цілі як у прев'ю бою й рівень монстра окремо; null — ціль уже
        /// зникла з мапи. У села рівня немає.
        /// </summary>
        private async Task<(string? Name, int? Level)> ResolveTargetAsync(March march, CancellationToken cancellationToken)
        {
            switch (march.TargetType)
            {
                case MarchTargetType.Monster:
                    var monster = await _monsterRepository.GetByIdAsync(march.TargetId, cancellationToken);

                    return monster is null ? (null, null) : (_catalog.MonsterName(monster.Type), monster.Level);

                case MarchTargetType.Village:
                    var village = await _villageRepository.GetByIdAsync(march.TargetId, cancellationToken);

                    return (village?.Name, null);

                default:
                    return (null, null);
            }
        }
    }
}
