using EmpireIdle.Application.Common.Services;
using EmpireIdle.Application.Interfaces;
using EmpireIdle.Domain.Entities;
using EmpireIdle.Domain.Services;
using Microsoft.Extensions.Logging;

namespace EmpireIdle.Application.Marches.Services
{
    /// <summary>
    /// Наслідки програної оборони (GDD §2.6): поразка пошкоджує будівлі,
    /// а серія поразок без повного відновлення виселяє село — у випадкову
    /// вільну клітину того ж кільця, під щит. Рахується будь-яка поразка,
    /// від будь-кого: захист від каруселі дає довгий щит, а не пороги.
    /// </summary>
    public sealed class CityFallService
    {
        private readonly IVillageFallRepository _fallRepository;
        private readonly IMapRepository _mapRepository;
        private readonly IServerRepository _serverRepository;
        private readonly IRandomSource _random;
        private readonly GameCatalog _catalog;
        private readonly SettlementPlacer _placer;
        private readonly WorldGeometry _geometry;
        private readonly EffectResolver _effectResolver;
        private readonly VillageRelocator _relocator;
        private readonly CityFallRules _rules;
        private readonly ILogger<CityFallService> _logger;

        public CityFallService(
            IVillageFallRepository fallRepository,
            IMapRepository mapRepository,
            IServerRepository serverRepository,
            IRandomSource random,
            GameCatalog catalog,
            SettlementPlacer placer,
            WorldGeometry geometry,
            EffectResolver effectResolver,
            VillageRelocator relocator,
            CityFallRules rules,
            ILogger<CityFallService> logger)
        {
            _fallRepository = fallRepository;
            _mapRepository = mapRepository;
            _serverRepository = serverRepository;
            _random = random;
            _catalog = catalog;
            _placer = placer;
            _geometry = geometry;
            _effectResolver = effectResolver;
            _relocator = relocator;
            _rules = rules;
            _logger = logger;
        }

        /// <summary>Пошкоджує будівлі переможеного й виселяє, якщо серія дійшла до межі.</summary>
        /// <returns>Запис падіння або null, якщо село лишилось на місці.</returns>
        public async Task<VillageFall?> SufferDefeatAsync(Village attacker, Village target, DateTime utcNow,
            CancellationToken cancellationToken)
        {
            if (!_rules.IsEnabled)
                return null;

            var serverLevel = await _serverRepository.GetLevelAsync(target.ServerId, cancellationToken);
            var boost = await _effectResolver.GetProductionBoostAsync(target.PlayerId, utcNow, cancellationToken);
            var locationMultiplier = _geometry.ProductionMultiplierAt(target.X, target.Y, serverLevel);

            var damaged = _rules.PickDamaged(target, firstInStreak: target.DefeatStreakAt(utcNow) == 0, _random);

            var streak = target.SufferDefeat(damaged, _catalog.Buildings, utcNow, _rules.RepairDuration,
                _rules.DamagedProductionMultiplier, boost, locationMultiplier);

            _logger.LogInformation("Village {VillageId} lost its defence: {Count} buildings damaged, streak {Streak}.",
                target.Id, damaged.Count, streak);

            if (!_rules.Evicts(streak))
                return null;

            return await EvictAsync(attacker, target, serverLevel, utcNow, cancellationToken);
        }

        private async Task<VillageFall?> EvictAsync(Village attacker, Village target, int serverLevel, DateTime utcNow,
            CancellationToken cancellationToken)
        {
            var ring = _geometry.RingAt(target.X, target.Y, serverLevel);

            var spot = await _placer.FindSpotNearRingAsync(target.ServerId, serverLevel, ring,
                (x, y) => _mapRepository.IsOccupiedAsync(target.ServerId, x, y, cancellationToken));

            if (spot is not { } cell)
            {
                _logger.LogWarning("Village {VillageId} should fall, but no free cell was found.", target.Id);
                return null;
            }

            var (fromX, fromY) = (target.X, target.Y);

            await _relocator.RelocateAsync(target, cell.X, cell.Y, utcNow, cancellationToken);

            var fall = new VillageFall(Guid.NewGuid(), target.ServerId, target.PlayerId, attacker.PlayerId, attacker.Name,
                fromX, fromY, cell.X, cell.Y, utcNow + _rules.ShieldDuration, utcNow);

            await _fallRepository.AddAsync(fall, cancellationToken);

            target.MarkFallen(fall, utcNow);

            _logger.LogInformation("Village {VillageId} fell to {AttackerId}: ({FromX},{FromY}) -> ({ToX},{ToY}).",
                target.Id, attacker.PlayerId, fromX, fromY, cell.X, cell.Y);

            return fall;
        }
    }
}
