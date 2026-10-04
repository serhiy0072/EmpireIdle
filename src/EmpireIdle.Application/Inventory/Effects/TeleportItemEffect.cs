using EmpireIdle.Application.Common.Services;
using EmpireIdle.Application.Interfaces;
using EmpireIdle.Application.Inventory.Contracts;
using EmpireIdle.Application.Territory.Services;
using EmpireIdle.Domain.Entities;
using EmpireIdle.Domain.Enums;
using EmpireIdle.Domain.Exceptions;
using EmpireIdle.Domain.Services;

namespace EmpireIdle.Application.Inventory.Effects
{
    /// <summary>
    /// Телепорт: переносить поселення (GDD §8.9). Тип задає конфіг предмета:
    /// точний — будь-яка обрана клітина; ближній — обрана клітина в радіусі від села;
    /// клановий — обрана клітина на території свого клану; випадковий — клітину обирає гра.
    /// </summary>
    public class TeleportItemEffect : IItemEffect
    {
        public string ItemType => "teleport";

        private readonly IVillageRepository _villageRepository;
        private readonly IMapRepository _mapRepository;
        private readonly IServerRepository _serverRepository;
        private readonly IClanRepository _clanRepository;
        private readonly IServerContext _serverContext;
        private readonly WorldGeometry _geometry;
        private readonly TerrainGenerator _terrain;
        private readonly SettlementPlacer _placer;
        private readonly TerritoryBonus _territory;
        private readonly VillageRelocator _relocator;

        public TeleportItemEffect(
            IVillageRepository villageRepository,
            IMapRepository mapRepository,
            IServerRepository serverRepository,
            IClanRepository clanRepository,
            IServerContext serverContext,
            WorldGeometry geometry,
            TerrainGenerator terrain,
            SettlementPlacer placer,
            TerritoryBonus territory,
            VillageRelocator relocator)
        {
            _villageRepository = villageRepository;
            _mapRepository = mapRepository;
            _serverRepository = serverRepository;
            _clanRepository = clanRepository;
            _serverContext = serverContext;
            _geometry = geometry;
            _terrain = terrain;
            _placer = placer;
            _territory = territory;
            _relocator = relocator;
        }

        public async Task ApplyAsync(ItemUsageContext context, CancellationToken cancellationToken)
        {
            // Один телепорт = один переїзд; Count > 1 спалив би зайві предмети без ефекту
            if (context.Count != 1)
                throw new RequirementNotMetException("Teleport is used one at a time.");

            var village = await _villageRepository.GetByPlayerIdAsync(context.PlayerId, cancellationToken)
                ?? throw new EntityNotFoundException("Village for player", context.PlayerId);

            var serverId = _serverContext.ServerId;
            var serverLevel = await _serverRepository.GetLevelAsync(serverId, cancellationToken);

            var (x, y) = context.Config.TeleportScope == TeleportScope.Random
                ? await _placer.FindSpotAsync(serverId, serverLevel,
                    (cx, cy) => _mapRepository.IsOccupiedAsync(serverId, cx, cy, cancellationToken))
                : await ChosenCellAsync(context, village, serverId, serverLevel, cancellationToken);

            await _relocator.RelocateAsync(village, x, y, context.UtcNow, cancellationToken);
        }

        /// <summary>Клітина, яку обрав гравець: спільні правила заселення плюс межа типу телепорта.</summary>
        private async Task<(int X, int Y)> ChosenCellAsync(ItemUsageContext context, Village village, int serverId,
            int serverLevel, CancellationToken cancellationToken)
        {
            if (context.TargetX is not { } x || context.TargetY is not { } y)
                throw new RequirementNotMetException("Teleport requires target coordinates.");

            var config = context.Config;

            // Межа типу — першою: гравцю корисніше «задалеко», ніж «клітина зайнята» за сотню клітин
            if (config.TeleportScope == TeleportScope.Nearby
                && Math.Max(Math.Abs(x - village.X), Math.Abs(y - village.Y)) > config.TeleportRange)
                throw new RequirementNotMetException(RefusalReasons.TeleportTooFar,
                    $"This teleport reaches {config.TeleportRange} cells.", config.TeleportRange);

            if (config.TeleportScope == TeleportScope.ClanTerritory)
            {
                if (await _clanRepository.GetClanIdByMemberAsync(context.PlayerId, cancellationToken) is null)
                    throw new RequirementNotMetException(RefusalReasons.TeleportNoClan, "A clan teleport needs a clan.");

                if (!await _territory.CoversAsync(context.PlayerId, x, y, context.UtcNow, cancellationToken))
                    throw new RequirementNotMetException(RefusalReasons.TeleportOutsideClanTerritory,
                        "That cell is outside your clan's territory.");
            }

            if (!_geometry.IsWithinFog(x, y, serverLevel))
                throw new RequirementNotMetException(RefusalReasons.TeleportOutsideRegion, "That cell is beyond the settled region.");

            if (!_terrain.IsHabitable(serverId, x, y))
                throw new RequirementNotMetException(RefusalReasons.TeleportCellUnsuitable, "That cell cannot hold a settlement.");

            if (await _mapRepository.IsOccupiedAsync(serverId, x, y, cancellationToken))
                throw new AlreadyExistsException(RefusalReasons.TeleportCellOccupied, "Map cell", $"({x},{y})");

            return (x, y);
        }
    }
}
