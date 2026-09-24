using EmpireIdle.Application.Common.Services;
using EmpireIdle.Application.Interfaces;
using EmpireIdle.Application.Inventory.Contracts;
using EmpireIdle.Domain.Exceptions;
using EmpireIdle.Domain.Services;

namespace EmpireIdle.Application.Inventory.Effects
{
    /// <summary>
    /// Телепорт: переносить поселення на обрану гравцем клітину.
    ///
    /// Клітину обирає гравець, а не гра: доступні позначає інтерфейс,
    /// і це знімає потребу в кількох типах телепорта — випадковому,
    /// точному й регіональному.
    /// </summary>
    public class TeleportItemEffect : IItemEffect
    {
        public string ItemType => "teleport";

        private readonly IVillageRepository _villageRepository;
        private readonly IMapRepository _mapRepository;
        private readonly IServerRepository _serverRepository;
        private readonly IServerContext _serverContext;
        private readonly WorldGeometry _geometry;
        private readonly TerrainGenerator _terrain;
        private readonly VillageRelocator _relocator;

        public TeleportItemEffect(
            IVillageRepository villageRepository,
            IMapRepository mapRepository,
            IServerRepository serverRepository,
            IServerContext serverContext,
            WorldGeometry geometry,
            TerrainGenerator terrain,
            VillageRelocator relocator)
        {
            _villageRepository = villageRepository;
            _mapRepository = mapRepository;
            _serverRepository = serverRepository;
            _serverContext = serverContext;
            _geometry = geometry;
            _terrain = terrain;
            _relocator = relocator;
        }

        public async Task ApplyAsync(ItemUsageContext context, CancellationToken cancellationToken)
        {
            // Один телепорт = один переїзд; Count > 1 спалив би зайві предмети без ефекту
            if (context.Count != 1)
                throw new RequirementNotMetException("Teleport is used one at a time.");

            if (context.TargetX is not { } x || context.TargetY is not { } y)
                throw new RequirementNotMetException("Teleport requires target coordinates.");

            var village = await _villageRepository.GetByPlayerIdAsync(context.PlayerId, cancellationToken)
                ?? throw new EntityNotFoundException("Village for player", context.PlayerId);

            var serverId = _serverContext.ServerId;
            var serverLevel = await _serverRepository.GetLevelAsync(serverId, cancellationToken);

            if (!_geometry.IsWithinFog(x, y, serverLevel))
                throw new RequirementNotMetException(RefusalReasons.TeleportOutsideRegion, "That cell is beyond the settled region.");

            if (!_terrain.IsHabitable(serverId, x, y))
                throw new RequirementNotMetException(RefusalReasons.TeleportCellUnsuitable, "That cell cannot hold a settlement.");

            if (await _mapRepository.IsOccupiedAsync(serverId, x, y, cancellationToken))
                throw new AlreadyExistsException(RefusalReasons.TeleportCellOccupied, "Map cell", $"({x},{y})");

            await _relocator.RelocateAsync(village, x, y, context.UtcNow, cancellationToken);
        }
    }
}
