using EmpireIdle.Application.Interfaces;
using EmpireIdle.Domain.Entities;
using EmpireIdle.Domain.Enums;
using EmpireIdle.Domain.Services;

namespace EmpireIdle.Application.Common.Services
{
    /// <summary>
    /// Механіка переїзду села на іншу клітину — спільна для телепорту й
    /// падіння міста. Чи можна туди переїхати, вирішує той, хто кличе:
    /// телепорт перевіряє вибір гравця, падіння обирає клітину саме.
    /// </summary>
    public sealed class VillageRelocator
    {
        private readonly IMapRepository _mapRepository;
        private readonly IMarchRepository _marchRepository;
        private readonly IGarrisonRepository _garrisonRepository;
        private readonly IServerRepository _serverRepository;
        private readonly GameCatalog _catalog;
        private readonly WorldGeometry _geometry;
        private readonly EffectResolver _effectResolver;

        public VillageRelocator(
            IMapRepository mapRepository,
            IMarchRepository marchRepository,
            IGarrisonRepository garrisonRepository,
            IServerRepository serverRepository,
            GameCatalog catalog,
            WorldGeometry geometry,
            EffectResolver effectResolver)
        {
            _mapRepository = mapRepository;
            _marchRepository = marchRepository;
            _garrisonRepository = garrisonRepository;
            _serverRepository = serverRepository;
            _catalog = catalog;
            _geometry = geometry;
            _effectResolver = effectResolver;
        }

        public async Task RelocateAsync(Village village, int x, int y, DateTime utcNow, CancellationToken cancellationToken)
        {
            var serverLevel = await _serverRepository.GetLevelAsync(village.ServerId, cancellationToken);

            // Фіксуємо буфери ДО зміни координат: множник кільця залежить від
            // позиції, і накопичене на околиці порахувалось би за новим
            var boost = await _effectResolver.GetProductionBoostAsync(village.PlayerId, utcNow, cancellationToken);
            var currentMultiplier = _geometry.ProductionMultiplierAt(village.X, village.Y, serverLevel);

            village.MaterializeProduction(_catalog.Buildings, utcNow, boost, currentMultiplier);

            var oldCell = await _mapRepository.GetByOccupantAsync(MapOccupantType.Village, village.Id, cancellationToken);
            if (oldCell is not null)
                _mapRepository.Remove(oldCell);

            village.RelocateTo(x, y, utcNow);

            // Гонку за останню клітину вирішує унікальний індекс (ServerId, X, Y),
            // а не перевірка того, хто кличе: між нею і вставкою може вклинитись інший
            await _mapRepository.AddAsync(
                new MapCell(Guid.NewGuid(), village.ServerId, x, y, MapOccupantType.Village, village.Id),
                cancellationToken);

            // Марші не блокують переїзд: армія в дорозі розвертається й повертається
            // за той самий час, що вже пройшла — на нові координати
            var garrison = await _garrisonRepository.GetByVillageIdAsync(village.Id, cancellationToken);

            if (garrison is not null)
            {
                var marches = await _marchRepository.GetActiveByGarrisonAsync(garrison.Id, cancellationToken);

                foreach (var march in marches)
                    march.RecallAfterRelocation(x, y, utcNow);
            }
        }
    }
}
