using EmpireIdle.Application.Common.Security;
using EmpireIdle.Application.Garrisons.ReadModels;
using EmpireIdle.Application.Interfaces;
using EmpireIdle.Domain.Services;
using MediatR;

namespace EmpireIdle.Application.Garrisons.Queries
{
    /// <summary>Запит на отримання гарнізону гравця в поданні для клієнта.</summary>
    public record GetGarrisonQuery(Guid PlayerId) : IRequest<GarrisonView>, IPlayerScopedRequest;

    public sealed class GetGarrisonQueryHandler : IRequestHandler<GetGarrisonQuery, GarrisonView>
    {
        private readonly IVillageRepository _villageRepository;
        private readonly IGarrisonRepository _garrisonRepository;
        private readonly GameCatalog _catalog;
        private readonly TimeProvider _timeProvider;
        private readonly SpeedUpCalculator _calculator;

        public GetGarrisonQueryHandler(
            IVillageRepository villageRepository,
            IGarrisonRepository garrisonRepository,
            GameCatalog catalog,
            TimeProvider timeProvider,
            SpeedUpCalculator calculator)
        {
            _villageRepository = villageRepository;
            _garrisonRepository = garrisonRepository;
            _catalog = catalog;
            _timeProvider = timeProvider;
            _calculator = calculator;
        }

        public async Task<GarrisonView> Handle(GetGarrisonQuery request, CancellationToken cancellationToken)
        {
            var now = _timeProvider.GetUtcNow().UtcDateTime;

            var village = await _villageRepository.GetByPlayerIdAsync(request.PlayerId, cancellationToken)
               ?? throw new InvalidOperationException($"Village not found for player {request.PlayerId}.");

            var garrison = await _garrisonRepository.GetByVillageIdReadOnlyAsync(village.Id, cancellationToken)
                ?? throw new InvalidOperationException($"Garrison not found for village {village.Id}.");

            // Прострочені не показуємо: вони вже недоступні для відновлення,
            // а рядки в БД прибирає окремий джоб
            var recoverable = garrison.Recoverable
                .Where(r => r.IsActive(now))
                .OrderBy(r => r.ExpiresAt)
                .Select(r => new RecoverableUnitView(
                    r.UnitType, r.Level, r.Count, r.ExpiresAt, RecoverCost(r.UnitType) * r.Count))
                .ToList();

            return new GarrisonView(
                garrison.Id,
                garrison.VillageId,
                garrison.Units.Select(u => new UnitView(u.UnitType, u.Level, u.Count)).ToList(),
                garrison.Wounded.Select(w => new UnitView(w.UnitType, w.Level, w.Count)).ToList(),
                recoverable,
                garrison.TrainingOrders.Select(o => new TrainingOrderView(
                    o.Id, o.UnitType, o.Level, o.Count, o.CompletesAt, _calculator.GetInstantFinishCost(o.CompletesAt, now))).ToList(),
                garrison.LevelUpOrders.Select(o => new LevelUpOrderView(
                    o.Id, o.UnitType, o.FromLevel, o.ToLevel, o.Count, o.CompletesAt, _calculator.GetInstantFinishCost(o.CompletesAt, now))).ToList());
        }

        private int RecoverCost(string unitType)
            => _catalog.Units.GetValueOrDefault(unitType)?.RecoverCostGems ?? 0;
    }
}
