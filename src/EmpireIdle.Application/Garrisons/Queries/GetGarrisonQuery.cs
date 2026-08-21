using EmpireIdle.Application.Common.Security;
using EmpireIdle.Application.Interfaces;
using EmpireIdle.Domain.Services;
using MediatR;

namespace EmpireIdle.Application.Garrisons.Queries
{
    /// <summary>Запит на отримання гарнізону гравця в поданні для клієнта.</summary>
    public record GetGarrisonQuery(Guid PlayerId) : IRequest<GarrisonView>, IPlayerScopedRequest;

    /// <summary>Гарнізон у поданні для клієнта.</summary>
    public record GarrisonView(
        Guid Id,
        Guid VillageId,
        List<UnitView> Units,
        List<UnitView> Wounded,
        List<RecoverableUnitView> Recoverable,
        List<TrainingOrderView> TrainingOrders);

    /// <summary>Юніти одного типу.</summary>
    public record UnitView(string UnitType, int Count);

    /// <summary>
    /// Юніти, яких ще можна відновити за gems. Ціна залежить від каталогу,
    /// а список відфільтрований за часом — тому збирається тут, не в контролері.
    /// </summary>
    public record RecoverableUnitView(string UnitType, int Count, DateTime ExpiresAt, int CostGems);

    /// <summary>Замовлення тренування в черзі.</summary>
    public record TrainingOrderView(Guid Id, string UnitType, int Count, DateTime CompletesAt);

    public sealed class GetGarrisonQueryHandler : IRequestHandler<GetGarrisonQuery, GarrisonView>
    {
        private readonly IVillageRepository _villageRepository;
        private readonly IGarrisonRepository _garrisonRepository;
        private readonly GameCatalog _catalog;
        private readonly TimeProvider _timeProvider;

        public GetGarrisonQueryHandler(
            IVillageRepository villageRepository,
            IGarrisonRepository garrisonRepository,
            GameCatalog catalog,
            TimeProvider timeProvider)
        {
            _villageRepository = villageRepository;
            _garrisonRepository = garrisonRepository;
            _catalog = catalog;
            _timeProvider = timeProvider;
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
                    r.UnitType, r.Count, r.ExpiresAt, RecoverCost(r.UnitType) * r.Count))
                .ToList();

            return new GarrisonView(
                garrison.Id,
                garrison.VillageId,
                garrison.Units.Select(u => new UnitView(u.UnitType, u.Count)).ToList(),
                garrison.Wounded.Select(w => new UnitView(w.UnitType, w.Count)).ToList(),
                recoverable,
                garrison.TrainingOrders
                    .Select(o => new TrainingOrderView(o.Id, o.UnitType, o.Count, o.CompletesAt))
                    .ToList());
        }

        private int RecoverCost(string unitType)
            => _catalog.Units.GetValueOrDefault(unitType)?.RecoverCostGems ?? 0;
    }
}
