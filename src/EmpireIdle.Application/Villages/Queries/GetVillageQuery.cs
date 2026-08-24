using EmpireIdle.Application.Common.Security;
using EmpireIdle.Application.Common.Services;
using EmpireIdle.Application.Interfaces;
using EmpireIdle.Domain.Exceptions;
using EmpireIdle.Domain.Services;
using MediatR;

namespace EmpireIdle.Application.Villages.Queries
{
    /// <summary>
    /// Запит на отримання села гравця в поданні для клієнта.
    /// </summary>
    public record GetVillageQuery(Guid PlayerId) : IRequest<VillageView>, IPlayerScopedRequest;

    /// <summary>Село в поданні для клієнта.</summary>
    public record VillageView(
        Guid Id,
        string Name,
        List<BuildingView> Buildings,
        List<ResourceView> Resources);

    /// <summary>
    /// Будівля з порахованим буфером. StoredAmount — величина на момент запиту,
    /// вона залежить від часу й буста, тому рахується тут, а не в контролері.
    /// </summary>
    public record BuildingView(
        Guid Id,
        string Type,
        int Level,
        DateTime LastCollectedAt,
        int StoredAmount,
        int StorageCap,
        DateTime? ConstructionCompletesAt,
        bool IsUnderConstruction);

    /// <summary>Ресурс села.</summary>
    public record ResourceView(string ResourceType, int Amount);

    /// <summary>
    /// Обробник запиту GetVillageQuery.
    /// </summary>
    public sealed class GetVillageQueryHandler : IRequestHandler<GetVillageQuery, VillageView>
    {
        private readonly IVillageRepository _villageRepository;
        private readonly EffectResolver _effectResolver;
        private readonly GameCatalog _catalog;
        private readonly TimeProvider _timeProvider;

        public GetVillageQueryHandler(
            IVillageRepository villageRepository,
            EffectResolver effectResolver,
            GameCatalog catalog,
            TimeProvider timeProvider)
        {
            _villageRepository = villageRepository;
            _effectResolver = effectResolver;
            _catalog = catalog;
            _timeProvider = timeProvider;
        }

        public async Task<VillageView> Handle(GetVillageQuery request, CancellationToken cancellationToken)
        {
            var now = _timeProvider.GetUtcNow().UtcDateTime;

            var village = await _villageRepository.GetByPlayerIdReadOnlyAsync(request.PlayerId, cancellationToken)
                ?? throw new InvalidOperationException($"Village for player with ID {request.PlayerId} not found.");

            var boost = await _effectResolver.GetProductionBoostAsync(request.PlayerId, now, cancellationToken);

            var buildings = village.Buildings.Select(b =>
            {
                // Тип без конфіга означає битий конфіг, але падати на GET села зайве:
                // гравець побачить будівлю з нульовим буфером, решта відповіді ціла
                if (!_catalog.Buildings.TryGetValue(b.Type, out var config))
                    return new BuildingView(b.Id, b.Type, b.Level.Value, b.LastCollectedAt,
                        StoredAmount: 0, StorageCap: 0, b.ConstructionCompletesAt, b.IsUnderConstruction);

                return new BuildingView(b.Id, b.Type, b.Level.Value, b.LastCollectedAt,
                    b.StoredAt(config, now, boost),
                    b.GetStorageCap(config.BaseStorage),
                    b.ConstructionCompletesAt, b.IsUnderConstruction);
            }).ToList();

            var resources = village.Resources
                .Select(r => new ResourceView(r.ResourceType, r.Amount))
                .ToList();

            return new VillageView(village.Id, village.Name, buildings, resources);
        }
    }
}
