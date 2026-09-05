using EmpireIdle.Application.Common.Services;
using EmpireIdle.Application.Interfaces;
using EmpireIdle.Domain.Entities;
using EmpireIdle.Domain.Enums;
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
        private readonly IMarchRepository _marchRepository;
        private readonly IGarrisonRepository _garrisonRepository;
        private readonly IServerRepository _serverRepository;
        private readonly IServerContext _serverContext;
        private readonly GameCatalog _catalog;
        private readonly WorldGeometry _geometry;
        private readonly TerrainGenerator _terrain;
        private readonly EffectResolver _effectResolver;

        public TeleportItemEffect(
            IVillageRepository villageRepository,
            IMapRepository mapRepository,
            IMarchRepository marchRepository,
            IGarrisonRepository garrisonRepository,
            IServerRepository serverRepository,
            IServerContext serverContext,
            GameCatalog catalog,
            WorldGeometry geometry,
            TerrainGenerator terrain,
            EffectResolver effectResolver)
        {
            _villageRepository = villageRepository;
            _mapRepository = mapRepository;
            _marchRepository = marchRepository;
            _garrisonRepository = garrisonRepository;
            _serverRepository = serverRepository;
            _serverContext = serverContext;
            _catalog = catalog;
            _geometry = geometry;
            _terrain = terrain;
            _effectResolver = effectResolver;
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
                throw new RequirementNotMetException("That cell is beyond the settled region.");

            if (!_terrain.IsHabitable(serverId, x, y))
                throw new RequirementNotMetException("That cell cannot hold a settlement.");

            if (await _mapRepository.IsOccupiedAsync(serverId, x, y, cancellationToken))
                throw new AlreadyExistsException("Map cell", $"({x},{y})");

            // Фіксуємо буфери ДО зміни координат: множник кільця залежить від
            // позиції, і накопичене на околиці порахувалось би за новим
            var boost = await _effectResolver.GetProductionBoostAsync(context.PlayerId, context.UtcNow, cancellationToken);
            var currentMultiplier = _geometry.ProductionMultiplierAt(village.X, village.Y, serverLevel);

            village.MaterializeProduction(_catalog.Buildings, context.UtcNow, boost, currentMultiplier);

            var oldCell = await _mapRepository.GetByOccupantAsync(MapOccupantType.Village, village.Id, cancellationToken);
            if (oldCell is not null)
                _mapRepository.Remove(oldCell);

            village.RelocateTo(x, y, context.UtcNow);

            // Гонку за останню клітину вирішує унікальний індекс (ServerId, X, Y),
            // а не перевірка вище: між нею і вставкою може вклинитись інший гравець
            await _mapRepository.AddAsync(
                new MapCell(Guid.NewGuid(), serverId, x, y, MapOccupantType.Village, village.Id),
                cancellationToken);

            // Марші не блокують переїзд: армія в дорозі розвертається й повертається
            // за той самий час, що вже пройшла — на нові координати
            var garrison = await _garrisonRepository.GetByVillageIdAsync(village.Id, cancellationToken);

            if (garrison is not null)
            {
                var marches = await _marchRepository.GetActiveByGarrisonAsync(garrison.Id, cancellationToken);

                foreach (var march in marches)
                    march.RecallAfterRelocation(x, y, context.UtcNow);
            }
        }
    }
}
