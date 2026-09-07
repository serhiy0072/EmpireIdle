using EmpireIdle.Application.Common.Services;
using EmpireIdle.Application.Interfaces;
using EmpireIdle.Domain.Entities;
using EmpireIdle.Domain.Enums;
using EmpireIdle.Domain.Services;
using EmpireIdle.Domain.Services.Config;
using Microsoft.Extensions.Logging;

namespace EmpireIdle.Application.Marches.Services
{
    /// <summary>
    /// Бій за село гравця.
    ///
    /// Найскладніший зі сценаріїв прибуття: захисник — це кілька власників
    /// одразу, тож втрати, поранені й звіти адресні. Господар бачить свій
    /// бій, союзник отримує назад те, що від нього лишилось, у власний
    /// госпіталь.
    /// </summary>
    public sealed class VillageBattleService
    {
        private readonly IGarrisonRepository _garrisonRepository;
        private readonly IVillageRepository _villageRepository;
        private readonly IBattleReportRepository _battleReportRepository;
        private readonly IServerRepository _serverRepository;
        private readonly IGameNotifier _notifier;
        private readonly IRandomSource _random;
        private readonly GameCatalog _catalog;
        private readonly CombatConfig _combatConfig;
        private readonly BattleResolver _resolver;
        private readonly DefenceLossAllocator _lossAllocator;
        private readonly CasualtySplitter _casualties;
        private readonly EffectResolver _effectResolver;
        private readonly WorldGeometry _geometry;
        private readonly MarchLogistics _logistics;
        private readonly ILogger<VillageBattleService> _logger;

        public VillageBattleService(
            IGarrisonRepository garrisonRepository,
            IVillageRepository villageRepository,
            IBattleReportRepository battleReportRepository,
            IServerRepository serverRepository,
            IGameNotifier notifier,
            IRandomSource random,
            GameCatalog catalog,
            BattleResolver resolver,
            DefenceLossAllocator lossAllocator,
            CasualtySplitter casualties,
            EffectResolver effectResolver,
            WorldGeometry geometry,
            MarchLogistics logistics,
            ILogger<VillageBattleService> logger)
        {
            _garrisonRepository = garrisonRepository;
            _villageRepository = villageRepository;
            _battleReportRepository = battleReportRepository;
            _serverRepository = serverRepository;
            _notifier = notifier;
            _random = random;
            _catalog = catalog;
            _combatConfig = catalog.Config.Combat;
            _resolver = resolver;
            _lossAllocator = lossAllocator;
            _casualties = casualties;
            _effectResolver = effectResolver;
            _geometry = geometry;
            _logistics = logistics;
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

            var shieldLevel = _combatConfig.NewbieShieldTownHallLevel;

            // Щит міг з'явитись хіба що в нападника, але село могло й зникнути.
            // Це прогін сканера, тож будь-яка невідповідність — розворот, не виняток
            if (targetVillage is null || targetGarrison is null
                || targetVillage.IsShielded(_catalog.Buildings, shieldLevel)
                || attackerVillage.IsShielded(_catalog.Buildings, shieldLevel))
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

            var defenderBonus = targetVillage.DefenceMultiplier(_catalog.Buildings);

            var seed = _random.Next(int.MaxValue);

            var attackerWoundedCapacity = _logistics.CalculateWoundedCapacity(attackerVillage, attackerGarrison);

            var outcome = _resolver.Resolve(attackerArmy, defenderArmy, terrain, seed,
                attackerBonus, defenderBonus, attackerWoundedCapacity);

            var result = outcome.Battle;

            march.ApplyLosses(result.AttackerLosses, utcNow);
            attackerGarrison.AdmitWounded(outcome.AttackerCasualties.Wounded, utcNow);

            var defenderLosses = _lossAllocator.Allocate(defence, result.DefenderLosses);

            targetGarrison.ApplyDefenceLosses(defenderLosses, utcNow);

            await AdmitAlliedWoundedAsync(defenderLosses, seed, utcNow, cancellationToken);

            if (result.AttackerWon)
                await PlunderAsync(march, targetVillage, utcNow, cancellationToken);

            await WriteReportsAsync(march, attackerVillage, attackerGarrison,
                targetVillage, targetGarrison, attackerArmy, defence, outcome, defenderLosses,
                terrain, seed, utcNow, cancellationToken);

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

            var loot = target.Plunder(_catalog.Buildings, capacity, boost, locationMultiplier, utcNow);

            if (loot.Count == 0)
                return;

            march.LoadCargo(loot, utcNow);

            _logger.LogInformation("March {MarchId} plundered {Amount} resources from village {VillageId}.",
                march.Id, loot.Values.Sum(), target.Id);
        }

        /// <summary>
        /// Розводить поранених підкріплень по госпіталях їхніх власників.
        /// Кожен платить за своїх, і чужий госпіталь чужими не забивається.
        /// Поранені господаря лягають у WriteReportsAsync, разом із його звітом.
        /// </summary>
        private async Task AdmitAlliedWoundedAsync(IReadOnlyList<StackLoss> losses, int seed, DateTime utcNow,
            CancellationToken cancellationToken)
        {
            foreach (var group in losses.GroupBy(l => l.OwnerPlayerId))
            {
                if (group.Key is not { } ownerId)
                    continue;

                var byType = group
                    .GroupBy(l => l.UnitType)
                    .ToDictionary(g => g.Key, g => g.Sum(l => l.Lost));

                // Гарнізон союзника стоїть за сотню клітин, але транзакція одна
                var ownerVillage = await _villageRepository.GetByPlayerIdAsync(ownerId, cancellationToken);
                var ownerGarrison = ownerVillage is null
                    ? null
                    : await _garrisonRepository.GetByVillageIdAsync(ownerVillage.Id, cancellationToken);

                if (ownerVillage is null || ownerGarrison is null)
                    continue;

                var ownerCapacity = _logistics.CalculateWoundedCapacity(ownerVillage, ownerGarrison);
                var ownerSplit = _casualties.Split(byType, ownerCapacity, seed ^ 0x1B873593);

                ownerGarrison.AdmitWounded(ownerSplit.Wounded, utcNow);
            }
        }

        /// <summary>
        /// Пише звіти обом сторонам. Кожен бачить бій зі свого боку:
        /// нападник — що втратив зі свого маршу, захисник — що втратив
        /// власний гарнізон. Підкріплення в звіт господаря не потрапляють:
        /// це чужі юніти, і в рядках вони виглядали б як його власні.
        ///
        /// Союзники власного звіту не отримують, а отже й кошика
        /// відновлюваних — це свідоме спрощення: один бій інакше породжував
        /// би до двохсот звітів.
        /// </summary>
        private async Task WriteReportsAsync(
            March march,
            Village attackerVillage,
            Garrison attackerGarrison,
            Village defenderVillage,
            Garrison defenderGarrison,
            IReadOnlyDictionary<string, int> attackerArmy,
            IReadOnlyList<DefenceStack> defence,
            BattleOutcome outcome,
            IReadOnlyList<StackLoss> defenderLosses,
            string terrain,
            int seed,
            DateTime utcNow,
            CancellationToken cancellationToken)
        {
            var result = outcome.Battle;
            var attackerSplit = outcome.AttackerCasualties;
            var expiresAt = utcNow.AddHours(_combatConfig.RecoveryWindowHours);

            var attackerReport = new BattleReport(
                Guid.NewGuid(),
                attackerVillage.PlayerId,
                march.Id,
                march.TargetX, march.TargetY, terrain,
                defenderVillage.Name, defenderVillage.MainBuildingLevel(_catalog.Buildings),
                result.AttackerWon, result.AttackerPower, result.DefenderPower, seed, utcNow);

            foreach (var (unitType, sent) in attackerArmy)
            {
                attackerReport.AddLine(
                    unitType,
                    sent,
                    attackerSplit.Wounded.GetValueOrDefault(unitType),
                    attackerSplit.Recoverable.GetValueOrDefault(unitType),
                    attackerSplit.Dead.GetValueOrDefault(unitType));
            }

            await _battleReportRepository.AddAsync(attackerReport, cancellationToken);

            if (attackerSplit.Recoverable.Count > 0)
                attackerGarrison.AddRecoverable(attackerSplit.Recoverable, attackerReport.Id, expiresAt, utcNow);

            // Для захисника координати власні: бій ішов у нього вдома,
            // а «ціллю» в його звіті виступає нападник
            var defenderReport = new BattleReport(
                Guid.NewGuid(),
                defenderVillage.PlayerId,
                march.Id,
                defenderVillage.X, defenderVillage.Y, terrain,
                attackerVillage.Name, attackerVillage.MainBuildingLevel(_catalog.Buildings),
                !result.AttackerWon, result.AttackerPower, result.DefenderPower, seed, utcNow);

            var hostStood = defence
                .Where(s => s.OwnerPlayerId is null)
                .GroupBy(s => s.UnitType)
                .ToDictionary(g => g.Key, g => g.Sum(s => s.Count));

            var hostLost = defenderLosses
                .Where(l => l.OwnerPlayerId is null)
                .GroupBy(l => l.UnitType)
                .ToDictionary(g => g.Key, g => g.Sum(l => l.Lost));

            var hostCapacity = _logistics.CalculateWoundedCapacity(defenderVillage, defenderGarrison);
            var hostSplit = _casualties.Split(hostLost, hostCapacity, seed ^ 0x1B873593);

            defenderGarrison.AdmitWounded(hostSplit.Wounded, utcNow);

            foreach (var (unitType, stood) in hostStood)
            {
                defenderReport.AddLine(
                    unitType,
                    stood,
                    hostSplit.Wounded.GetValueOrDefault(unitType),
                    hostSplit.Recoverable.GetValueOrDefault(unitType),
                    hostSplit.Dead.GetValueOrDefault(unitType));
            }

            await _battleReportRepository.AddAsync(defenderReport, cancellationToken);

            if (hostSplit.Recoverable.Count > 0)
                defenderGarrison.AddRecoverable(hostSplit.Recoverable, defenderReport.Id, expiresAt, utcNow);

            march.RecordBattle(attackerVillage.PlayerId, attackerReport.Id, result.AttackerWon,
                defenderVillage.Name, utcNow);

            await _notifier.NotifyBattleFinishedAsync(defenderVillage.PlayerId, defenderReport.Id,
                !result.AttackerWon, attackerVillage.Name, cancellationToken);
        }
    }
}
