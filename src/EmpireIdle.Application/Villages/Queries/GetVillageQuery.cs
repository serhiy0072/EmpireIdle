using EmpireIdle.Application.Common.Security;
using EmpireIdle.Application.Common.Services;
using EmpireIdle.Application.Interfaces;
using EmpireIdle.Application.Villages.ReadModels;
using EmpireIdle.Domain.Services;
using MediatR;
using System.Net.NetworkInformation;

namespace EmpireIdle.Application.Villages.Queries
{
    /// <summary>
    /// Запит на отримання села гравця в поданні для клієнта.
    /// </summary>
    public record GetVillageQuery(Guid PlayerId) : IRequest<VillageView>, IPlayerScopedRequest;

    /// <summary>
    /// Обробник запиту GetVillageQuery.
    /// </summary>
    public sealed class GetVillageQueryHandler : IRequestHandler<GetVillageQuery, VillageView>
    {
        private readonly IVillageRepository _villageRepository;
        private readonly IServerRepository _serverRepository;
        private readonly EffectResolver _effectResolver;
        private readonly GameCatalog _catalog;
        private readonly TimeProvider _timeProvider;
        private readonly WorldGeometry _geometry;
        private readonly VillageStatus _status;
        private readonly SpeedUpCalculator _calculator;
        private readonly CityFallRules _cityFall;

        public GetVillageQueryHandler(
            IVillageRepository villageRepository,
            IServerRepository serverRepository,
            EffectResolver effectResolver,
            GameCatalog catalog,
            TimeProvider timeProvider,
            WorldGeometry geometry,
            VillageStatus status,
            SpeedUpCalculator calculator,
            CityFallRules cityFall)
        {
            _villageRepository = villageRepository;
            _serverRepository = serverRepository;
            _effectResolver = effectResolver;
            _catalog = catalog;
            _timeProvider = timeProvider;
            _geometry = geometry;
            _status = status;
            _calculator = calculator;
            _cityFall = cityFall;
        }

        public async Task<VillageView> Handle(GetVillageQuery request, CancellationToken cancellationToken)
        {
            var now = _timeProvider.GetUtcNow().UtcDateTime;

            var village = await _villageRepository.GetByPlayerIdReadOnlyAsync(request.PlayerId, cancellationToken)
                ?? throw new InvalidOperationException($"Village for player with ID {request.PlayerId} not found.");

            var boost = await _effectResolver.GetProductionBoostAsync(request.PlayerId, now, cancellationToken);

            var serverLevel = await _serverRepository.GetLevelAsync(village.ServerId, cancellationToken);

            var buildings = village.Buildings.Select(b =>
            {
                var isUnlocekd = _status.IsUnlocked(village, b.Type);

                // Тип без конфіга означає битий конфіг, але падати на GET села зайве:
                // гравець побачить будівлю з нульовим буфером, решта відповіді ціла
                int? speedUpCost = b.IsUnderConstruction
                    ? _calculator.GetInstantFinishCost(b.ConstructionCompletesAt!.Value, now)
                    : null;

                if (!_catalog.Buildings.TryGetValue(b.Type, out var config))
                {
                    return new BuildingView(
                        b.Id,
                        b.Type,
                        b.Level.Value,
                        b.LastCollectedAt,
                        StoredAmount: 0,
                        StorageCap: 0,
                        b.ConstructionCompletesAt,
                        b.IsUnderConstruction,
                        isUnlocekd,
                        speedUpCost);
                }

                var locationMultiplier = _geometry.ProductionMultiplierAt(village.X, village.Y, serverLevel);

                // Під туманом буфер не росте: число з формули було б фантомом
                var stored = village.IsProducing(b, _catalog.Buildings)
                    ? b.StoredAt(config, now, boost, locationMultiplier)
                    : 0;

                return new BuildingView(
                    b.Id,
                    b.Type,
                    b.Level.Value,
                    b.LastCollectedAt,
                    stored,
                    b.GetStorageCap(config.BaseStorage),
                    b.ConstructionCompletesAt,
                    b.IsUnderConstruction,
                    isUnlocekd,
                    speedUpCost,
                    b.DamageAt(now),
                    b.IsDamagedAt(now) ? b.DamagedUntil : null);
            }).ToList();

            var resources = village.Resources
                .Select(r => new ResourceView(r.ResourceType, r.Amount, _status.IsResourceUnlocked(village, r.ResourceType)))
                .ToList();

            // Лише поки щось пошкоджене: після самовідновлення серії вже немає
            var damage = village.HasDamageAt(now)
                ? new VillageDamageView(
                    village.DefeatStreakAt(now),
                    _cityFall.DefeatsToEvict,
                    village.RepairCost(_catalog.Buildings, now, _cityFall.RepairCostShare)
                        .Select(c => new RepairCostView(c.Resource, c.Amount))
                        .ToList())
                : null;

            return new VillageView(village.Id, village.Name, village.X, village.Y, buildings, resources,
                village.IsShieldedAt(now) ? village.ShieldUntil : null, damage);
        }
    }
}
