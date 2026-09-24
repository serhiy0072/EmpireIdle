using EmpireIdle.Application.Common.Services;
using EmpireIdle.Application.Interfaces;
using EmpireIdle.Domain.Entities;
using EmpireIdle.Domain.Enums;
using EmpireIdle.Domain.Services;
using Microsoft.Extensions.Logging;

namespace EmpireIdle.Application.Marches.Services
{
    /// <summary>
    /// Падіння міста (GDD §2.6): переможний бій за село може його виселити —
    /// у випадкову вільну клітину того ж кільця, під щит.
    ///
    /// Щит перевіряється раніше, ще до бою; тут — решта запобіжників:
    /// співвідношення сил і ліміт виселень нападника за вікно.
    /// </summary>
    public sealed class CityFallService
    {
        private readonly IPlayerPowerRepository _powerRepository;
        private readonly IVillageFallRepository _fallRepository;
        private readonly IMapRepository _mapRepository;
        private readonly IServerRepository _serverRepository;
        private readonly SettlementPlacer _placer;
        private readonly WorldGeometry _geometry;
        private readonly VillageRelocator _relocator;
        private readonly CityFallRules _rules;
        private readonly ILogger<CityFallService> _logger;

        public CityFallService(
            IPlayerPowerRepository powerRepository,
            IVillageFallRepository fallRepository,
            IMapRepository mapRepository,
            IServerRepository serverRepository,
            SettlementPlacer placer,
            WorldGeometry geometry,
            VillageRelocator relocator,
            CityFallRules rules,
            ILogger<CityFallService> logger)
        {
            _powerRepository = powerRepository;
            _fallRepository = fallRepository;
            _mapRepository = mapRepository;
            _serverRepository = serverRepository;
            _placer = placer;
            _geometry = geometry;
            _relocator = relocator;
            _rules = rules;
            _logger = logger;
        }

        /// <summary>
        /// Виселяє село, якщо запобіжники пропускають. Сили — збережені до бою:
        /// перерахунок після втрат прийде подією пізніше, і рішення про падіння
        /// не має залежати від того, хто встиг першим.
        /// </summary>
        /// <returns>Запис падіння або null, якщо села не виселено.</returns>
        public async Task<VillageFall?> TryEvictAsync(Village attacker, Village target, DateTime utcNow,
            CancellationToken cancellationToken)
        {
            if (!_rules.IsEnabled)
                return null;

            var powers = await _powerRepository.GetTotalPowerAsync([attacker.PlayerId, target.PlayerId], cancellationToken);
            var recent = await _fallRepository.CountByAttackerSinceAsync(attacker.PlayerId, _rules.WindowStart(utcNow), cancellationToken);

            var verdict = _rules.Judge(
                powers.GetValueOrDefault(attacker.PlayerId),
                powers.GetValueOrDefault(target.PlayerId),
                recent);

            if (verdict != CityFallVerdict.Evict)
            {
                _logger.LogInformation("Village {VillageId} held after defeat: {Verdict}.", target.Id, verdict);
                return null;
            }

            var serverLevel = await _serverRepository.GetLevelAsync(target.ServerId, cancellationToken);
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
