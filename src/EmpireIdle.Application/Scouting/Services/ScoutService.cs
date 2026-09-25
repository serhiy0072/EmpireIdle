using EmpireIdle.Application.Common.Services;
using EmpireIdle.Application.Interfaces;
using EmpireIdle.Application.Marches.Services;
using EmpireIdle.Domain.Combat;
using EmpireIdle.Domain.Entities;
using EmpireIdle.Domain.Enums;
using EmpireIdle.Domain.Services;
using Microsoft.Extensions.Logging;

namespace EmpireIdle.Application.Scouting.Services
{
    /// <summary>
    /// Розвідники дійшли: знімок цілі в звіт. Сила оборони — та сама, що побачив би бій
    /// (MarchTargetResolver + CombatCalculator: бусти, територія клану, лідери); здобич —
    /// за тими самими правилами, що й грабунок. Інакше звіт обманював би того, хто за ним нападе.
    /// </summary>
    public sealed class ScoutService
    {
        private readonly IGarrisonRepository _garrisonRepository;
        private readonly IVillageRepository _villageRepository;
        private readonly IClanStructureRepository _structureRepository;
        private readonly IServerRepository _serverRepository;
        private readonly IScoutReportRepository _reports;
        private readonly IGameNotifier _notifier;
        private readonly MarchTargetResolver _targets;
        private readonly ScoutVisibility _visibility;
        private readonly CombatCalculator _combat;
        private readonly PlunderCalculator _plunder;
        private readonly EffectResolver _effectResolver;
        private readonly WorldGeometry _geometry;
        private readonly ILogger<ScoutService> _logger;

        public ScoutService(
            IGarrisonRepository garrisonRepository,
            IVillageRepository villageRepository,
            IClanStructureRepository structureRepository,
            IServerRepository serverRepository,
            IScoutReportRepository reports,
            IGameNotifier notifier,
            MarchTargetResolver targets,
            ScoutVisibility visibility,
            CombatCalculator combat,
            PlunderCalculator plunder,
            EffectResolver effectResolver,
            WorldGeometry geometry,
            ILogger<ScoutService> logger)
        {
            _garrisonRepository = garrisonRepository;
            _villageRepository = villageRepository;
            _structureRepository = structureRepository;
            _serverRepository = serverRepository;
            _reports = reports;
            _notifier = notifier;
            _targets = targets;
            _visibility = visibility;
            _combat = combat;
            _plunder = plunder;
            _effectResolver = effectResolver;
            _geometry = geometry;
            _logger = logger;
        }

        public async Task ResolveAsync(March march, string terrain, DateTime utcNow, CancellationToken cancellationToken)
        {
            var garrison = await _garrisonRepository.GetByIdAsync(march.GarrisonId, cancellationToken)
                ?? throw new InvalidOperationException($"Garrison {march.GarrisonId} not found for march {march.Id}.");

            var scouter = await _villageRepository.GetByIdAsync(garrison.VillageId, cancellationToken)
                ?? throw new InvalidOperationException($"Village {garrison.VillageId} not found.");

            var report = march.TargetType == MarchTargetType.Village
                ? await ScoutVillageAsync(march, scouter, terrain, utcNow, cancellationToken)
                : await ScoutStructureAsync(march, scouter, terrain, utcNow, cancellationToken);

            march.FinishScouting(utcNow);

            await _reports.AddAsync(report, cancellationToken);
            await _notifier.NotifyScoutReportReadyAsync(scouter.PlayerId, report.Id, report.TargetName,
                report.Outcome.ToString(), cancellationToken);

            _logger.LogInformation("Scouts {MarchId} of {PlayerId} at ({X},{Y}): {Outcome}",
                march.Id, scouter.PlayerId, march.TargetX, march.TargetY, report.Outcome);
        }

        private async Task<ScoutReport> ScoutVillageAsync(March march, Village scouter, string terrain,
            DateTime utcNow, CancellationToken cancellationToken)
        {
            var village = await _villageRepository.GetByIdAsync(march.TargetId, cancellationToken);

            if (village is null)
                return ScoutReport.Failed(Guid.NewGuid(), march.ServerId, scouter.PlayerId, march, string.Empty,
                    ScoutOutcome.TargetGone, utcNow);

            // Переселилась, поки йшли розвідники, — на старому місці нікого
            if (village.X != march.TargetX || village.Y != march.TargetY)
                return ScoutReport.Failed(Guid.NewGuid(), march.ServerId, scouter.PlayerId, march, village.Name,
                    ScoutOutcome.TargetMoved, utcNow);

            var target = await _targets.ResolveAsync(MarchTargetType.Village, village.Id, scouter, utcNow, cancellationToken);

            // Завісу могли накинути вже після відправки
            if (await _visibility.IsHiddenAsync(target, utcNow, cancellationToken))
                return ScoutReport.Failed(Guid.NewGuid(), march.ServerId, scouter.PlayerId, march, village.Name,
                    ScoutOutcome.Blocked, utcNow);

            // Буст і множник кільця — цілі: буфери, які ми рахуємо, вироблені її селом
            var boost = await _effectResolver.GetProductionBoostAsync(village.PlayerId, utcNow, cancellationToken);
            var serverLevel = await _serverRepository.GetLevelAsync(village.ServerId, cancellationToken);
            var lootable = _plunder.Lootable(village, boost,
                _geometry.ProductionMultiplierAt(village.X, village.Y, serverLevel), utcNow);

            return ScoutReport.Success(Guid.NewGuid(), march.ServerId, scouter.PlayerId, march, village.Name,
                DefencePower(target, terrain), lootable, utcNow);
        }

        private async Task<ScoutReport> ScoutStructureAsync(March march, Village scouter, string terrain,
            DateTime utcNow, CancellationToken cancellationToken)
        {
            var structure = await _structureRepository.GetByIdAsync(march.TargetId, cancellationToken);

            if (structure is null)
                return ScoutReport.Failed(Guid.NewGuid(), march.ServerId, scouter.PlayerId, march, string.Empty,
                    ScoutOutcome.TargetGone, utcNow);

            var target = await _targets.ResolveAsync(MarchTargetType.ClanStructure, structure.Id, scouter, utcNow, cancellationToken);

            // Споруда не грабується — лише сила гарнізону
            return ScoutReport.Success(Guid.NewGuid(), march.ServerId, scouter.PlayerId, march, target.Name,
                DefencePower(target, terrain), new Dictionary<string, int>(), utcNow);
        }

        /// <summary>Та сама сила, що в прев'ю й бою, без кидка кубика.</summary>
        private double DefencePower(MarchTarget target, string terrain)
            => _combat.CalculateDefencePower(target.Defence, terrain, target.DefenceBuffs) * target.DefenceMultiplier;
    }
}
