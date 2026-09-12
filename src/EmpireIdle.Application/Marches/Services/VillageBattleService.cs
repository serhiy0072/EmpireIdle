using EmpireIdle.Application.Common.Services;
using EmpireIdle.Application.Interfaces;
using EmpireIdle.Domain.Entities;
using EmpireIdle.Domain.Enums;
using EmpireIdle.Domain.Services;
using EmpireIdle.Domain.Services.Config;
using Microsoft.Extensions.Logging;
using System.Net.NetworkInformation;

namespace EmpireIdle.Application.Marches.Services
{
    /// <summary>
    /// Бій за село гравця.
    ///
    /// Найскладніший зі сценаріїв прибуття: захисник — це кілька власників
    /// одразу, тож втрати розподіляються по стеках, а наслідки записуються
    /// кожному окремо. Сам запис — у BattleAftermath, тут лишається бій.
    /// </summary>
    public sealed class VillageBattleService
    {
        private readonly IGarrisonRepository _garrisonRepository;
        private readonly IVillageRepository _villageRepository;
        private readonly IServerRepository _serverRepository;
        private readonly IRandomSource _random;
        private readonly BattleResolver _resolver;
        private readonly DefenceLossAllocator _lossAllocator;
        private readonly EffectResolver _effectResolver;
        private readonly WorldGeometry _geometry;
        private readonly MarchLogistics _logistics;
        private readonly BattleAftermath _aftermath;
        private readonly VillageStatus _status;
        private readonly PlunderCalculator _plunder;
        private readonly ILogger<VillageBattleService> _logger;

        public VillageBattleService(
            IGarrisonRepository garrisonRepository,
            IVillageRepository villageRepository,
            IServerRepository serverRepository,
            IRandomSource random,
            GameCatalog catalog,
            BattleResolver resolver,
            DefenceLossAllocator lossAllocator,
            EffectResolver effectResolver,
            WorldGeometry geometry,
            MarchLogistics logistics,
            BattleAftermath aftermath,
            VillageStatus status,
            PlunderCalculator plunder,
            ILogger<VillageBattleService> logger)
        {
            _garrisonRepository = garrisonRepository;
            _villageRepository = villageRepository;
            _serverRepository = serverRepository;
            _random = random;
            _resolver = resolver;
            _lossAllocator = lossAllocator;
            _effectResolver = effectResolver;
            _geometry = geometry;
            _logistics = logistics;
            _aftermath = aftermath;
            _status = status;
            _plunder = plunder;
            _logger = logger;
        }

        /// <summary>
        /// Проводить бій. Захисник — гарнізон господаря разом із підкріпленнями
        /// клану; втрати кожної сторони розкидаються по власниках, і поранені
        /// йдуть у госпіталь того, чиї це юніти, а не того, хто оборонявся.
        /// </summary>
        public async Task ResolveAsync(March march, Dictionary<string, int> attackerArmy,
            string terrain, DateTime utcNow, CancellationToken cancellationToken)
        {
            var attackerGarrison = await _garrisonRepository.GetByIdAsync(march.GarrisonId, cancellationToken)
                ?? throw new InvalidOperationException($"Garrison {march.GarrisonId} not found for march {march.Id}.");

            var attackerVillage = await _villageRepository.GetByIdAsync(attackerGarrison.VillageId, cancellationToken)
                ?? throw new InvalidOperationException($"Village {attackerGarrison.VillageId} not found.");

            var targetVillage = await _villageRepository.GetByIdAsync(march.TargetId, cancellationToken);
            var targetGarrison = targetVillage is null
                ? null
                : await _garrisonRepository.GetByVillageIdAsync(targetVillage.Id, cancellationToken);

            // Щит міг з'явитись хіба що в нападника, але село могло й зникнути.
            // Це прогін сканера, тож будь-яка невідповідність — розворот, не виняток
            if (targetVillage is null || targetGarrison is null
                || _status.IsShielded(targetVillage)
                || _status.IsShielded(attackerVillage))
            {
                _logistics.TurnMarchBack(march, attackerArmy, utcNow);
                return;
            }

            var defence = targetGarrison.GetDefence();
            var defenderArmy = defence
                .GroupBy(s => s.UnitType)
                .ToDictionary(g => g.Key, g => g.Sum(s => s.Count));

            var attackerBonus = await _effectResolver.GetMultiplierAsync(
                attackerVillage.PlayerId, EffectTarget.Attack, utcNow, cancellationToken);

            var defenderBonus = _status.DefenceMultiplier(targetVillage);

            // Сід фіксуємо до бою: він іде і в розрахунок, і у звіти обох сторін
            var seed = _random.Next(int.MaxValue);

            var attackerWoundedCapacity = _logistics.CalculateWoundedCapacity(attackerVillage, attackerGarrison);

            var outcome = _resolver.Resolve(attackerArmy, defenderArmy, terrain, seed,
                attackerBonus, defenderBonus, attackerWoundedCapacity);

            var result = outcome.Battle;

            march.ApplyLosses(result.AttackerLosses, utcNow);
            attackerGarrison.AdmitWounded(outcome.AttackerCasualties.Wounded, utcNow);

            var defenderLosses = _lossAllocator.Allocate(defence, result.DefenderLosses);

            targetGarrison.ApplyDefenceLosses(defenderLosses, utcNow);

            await _aftermath.AdmitAlliedWoundedAsync(defenderLosses, seed, utcNow, cancellationToken);

            // Грабунок до звітів: вантаж має бути на марші, коли той його згадає
            if (result.AttackerWon)
                await PlunderAsync(march, targetVillage, utcNow, cancellationToken);

            await _aftermath.RecordAttackerAsync(march, attackerVillage, attackerGarrison,
                targetVillage.Name, _status.MainBuildingLevel(targetVillage),
                attackerArmy, outcome, terrain, seed, utcNow, cancellationToken);

            await _aftermath.RecordDefenderAsync(march, targetVillage, targetGarrison, attackerVillage,
                defence, defenderLosses, result, terrain, seed, utcNow, cancellationToken);

            _logistics.TurnMarchBack(march, march.GetUnits(), utcNow);

            _logger.LogInformation(
                "PvP at ({X},{Y}): {Attacker} vs {Defender}, attacker {Outcome}",
                march.TargetX, march.TargetY, attackerVillage.PlayerId, targetVillage.PlayerId,
                result.AttackerWon ? "won" : "lost");
        }

        /// <summary>
        /// Забирає здобич після переможного бою. Вантажопідйомність рахується
        /// по тих, хто пережив бій, тож великі втрати зменшують і винесене.
        /// </summary>
        private async Task PlunderAsync(March march, Village target, DateTime utcNow, CancellationToken cancellationToken)
        {
            var capacity = _logistics.CalculateCarryCapacity(march.GetUnits());

            if (capacity <= 0)
                return;

            // Буст і множник кільця — захисника, не нападника: буфери,
            // які ми зараз матеріалізуємо, вироблені його селом
            var boost = await _effectResolver.GetProductionBoostAsync(target.PlayerId, utcNow, cancellationToken);
            var serverLevel = await _serverRepository.GetLevelAsync(target.ServerId, cancellationToken);
            var locationMultiplier = _geometry.ProductionMultiplierAt(target.X, target.Y, serverLevel);

            var loot = _plunder.Plunder(target, capacity, boost, locationMultiplier, utcNow);

            if (loot.Count == 0)
                return;

            march.LoadCargo(loot, utcNow);

            _logger.LogInformation("March {MarchId} plundered {Amount} resources from village {VillageId}.",
                march.Id, loot.Values.Sum(), target.Id);
        }
    }
}
