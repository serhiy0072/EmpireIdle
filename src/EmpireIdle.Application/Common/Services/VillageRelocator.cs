using EmpireIdle.Application.Clans.Services;
using EmpireIdle.Application.Interfaces;
using EmpireIdle.Application.Marches.Services;
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
        private readonly IHeroRepository _heroRepository;
        private readonly IServerRepository _serverRepository;
        private readonly GameCatalog _catalog;
        private readonly WorldGeometry _geometry;
        private readonly EffectResolver _effectResolver;
        private readonly MarchHomecoming _homecoming;
        private readonly ReinforcementReturner _reinforcements;

        public VillageRelocator(
            IMapRepository mapRepository,
            IMarchRepository marchRepository,
            IGarrisonRepository garrisonRepository,
            IHeroRepository heroRepository,
            IServerRepository serverRepository,
            GameCatalog catalog,
            WorldGeometry geometry,
            EffectResolver effectResolver,
            MarchHomecoming homecoming,
            ReinforcementReturner reinforcements)
        {
            _mapRepository = mapRepository;
            _marchRepository = marchRepository;
            _garrisonRepository = garrisonRepository;
            _heroRepository = heroRepository;
            _serverRepository = serverRepository;
            _catalog = catalog;
            _geometry = geometry;
            _effectResolver = effectResolver;
            _homecoming = homecoming;
            _reinforcements = reinforcements;
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

            // Марші не блокують переїзд (§2.5): усі війська гравця — з маршів,
            // зі здобиччю чи без, і з чужих гарнізонів — одразу вдома
            var garrison = await _garrisonRepository.GetByVillageIdAsync(village.Id, cancellationToken);

            if (garrison is null)
                return;

            // Слот лідера рахуємо самі: герої прибувають в одній транзакції,
            // і незбереженого лідера база ще не бачить
            var leaderSlotFree = await _heroRepository.GetLeaderAsync(garrison.Id, village.PlayerId, cancellationToken) is null;

            var marches = await _marchRepository.GetActiveByGarrisonAsync(garrison.Id, cancellationToken);

            foreach (var march in marches)
            {
                march.CallHomeAtOnce(x, y, utcNow);

                if (await _homecoming.ArriveAsync(march, garrison, leaderSlotFree, utcNow, cancellationToken))
                    leaderSlotFree = false;
            }

            await _reinforcements.BringAllOfPlayerHomeNowAsync(
                village.PlayerId, garrison, leaderSlotFree, utcNow, cancellationToken);
        }
    }
}
