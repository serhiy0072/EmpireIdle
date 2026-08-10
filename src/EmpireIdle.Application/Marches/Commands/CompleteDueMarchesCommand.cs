using EmpireIdle.Application.Common.Services;
using EmpireIdle.Application.Interfaces;
using EmpireIdle.Domain.Entities;
using EmpireIdle.Domain.Services;
using MediatR;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System.Text.RegularExpressions;
using static System.Net.WebRequestMethods;

namespace EmpireIdle.Application.Marches.Commands
{
    /// <summary>Обробляє походи, чий час прибуття настав.</summary>
    public record CompleteDueMarchesCommand : IRequest;

    public class CompleteDueMarchesCommandHandler : IRequestHandler<CompleteDueMarchesCommand>
    {
        private const int ServerId = 1;

        private readonly IMarchRepository _marchRepository;
        private readonly IGarrisonRepository _garrisonRepository;
        private readonly IUnitOfWork _unitOfWork;
        private readonly IMapRepository _mapRepository;
        private readonly IMonsterRepository _monsterRepository;
        private readonly IVillageRepository _villageRepository;
        private readonly IBattleReportRepository _battleReportRepository;
        private readonly GameConfig _gameConfig;
        private readonly CombatConfig _combatConfig;
        private readonly CasualtySplitter _casualties;
        private readonly MonsterArmyBuilder _armyBuilder;
        private readonly CombatCalculator _combat;
        private readonly TerrainGenerator _terrain;
        private readonly MarchCalculator _calculator;
        private readonly EffectResolver _effectResolver;
        private readonly ILogger<CompleteDueMarchesCommandHandler> _logger;

        public CompleteDueMarchesCommandHandler(
            IMarchRepository marchRepository,
            IGarrisonRepository garrisonRepository,
            IUnitOfWork unitOfWork,
            IMapRepository mapRepository,
            IMonsterRepository monsterRepository,   
            IVillageRepository villageRepository,
            IBattleReportRepository battleReportRepository,
            IOptions<GameConfig> gameConfig,
            CasualtySplitter casualties,
            MonsterArmyBuilder armyBuilder,
            CombatCalculator combat,
            TerrainGenerator terrain,
            MarchCalculator calculator,
            EffectResolver effectResolver,
            ILogger<CompleteDueMarchesCommandHandler> logger)
        {
            _marchRepository = marchRepository;
            _garrisonRepository = garrisonRepository;
            _unitOfWork = unitOfWork;
            _mapRepository = mapRepository;
            _monsterRepository = monsterRepository;
            _villageRepository = villageRepository;
            _battleReportRepository = battleReportRepository;
            _casualties = casualties;
            _armyBuilder = armyBuilder;
            _combat = combat;
            _terrain = terrain;
            _calculator = calculator;
            _effectResolver = effectResolver;
            _logger = logger;
            _gameConfig = gameConfig.Value;
            _combatConfig = _gameConfig.Combat;
        }

        public async Task Handle(CompleteDueMarchesCommand request, CancellationToken cancellationToken)
        {
            var now = DateTime.UtcNow;
            var due = await _marchRepository.GetDueAsync(now, cancellationToken);

            if (due.Count == 0)
                return;

            var battles = 0;
            var returned = 0;
            var failed = 0;

            foreach (var march in due)
            {
                try
                {
                    if (march.State == MarchState.Outbound)
                    {
                        await ResolveBattleAsync(march, cancellationToken);
                        battles++;
                    }
                    else if (march.State == MarchState.Returning)
                    {
                        var garrison = await _garrisonRepository.GetByIdAsync(march.GarrisonId, cancellationToken);
                        if (garrison is null)
                        {
                            _logger.LogWarning("Garrison {GarrisonId} not found for march {MarchId}", march.GarrisonId, march.Id);
                            continue;
                        }

                        var survivors = march.GetUnits();
                        if (survivors.Count > 0)
                            garrison.ReceiveUnits(survivors);

                        march.Complete();
                        returned++;
                    }

                    // Зберігаємо помарш: збій на одному не має відкочувати решту батча
                    await _unitOfWork.SaveChangesAsync(cancellationToken);
                }
                catch (Exception ex)
                {
                    // Викидаємо частковий стан впалого маршу, інакше він поїде з наступним SaveChanges
                    _unitOfWork.DiscardChanges();
                    failed++;

                    _logger.LogError(ex, "Failed to process march {MarchId} (state {State})", march.Id, march.State);
                }
            }
            if (failed > 0)
                _logger.LogWarning("Marches skipped due to errors: {Failed}", failed);
        }

        /// <summary>Проводить бій на місці прибуття армії.</summary>
        private async Task ResolveBattleAsync(March march, CancellationToken cancellationToken)
        {
            var attackerArmy = march.GetUnits();
            var terrain = _terrain.GetTerrainType(ServerId, march.TargetX, march.TargetY);

            if(march.TargetType != MarchTargetType.Monster)
            {
                // PvP — окрема фаза; поки армія просто розвертається
                TurnMarchBack(march, attackerArmy);
                return;
            }

            var monster = await _monsterRepository.GetByIdAsync(march.TargetId, cancellationToken);
            if (monster is null)
            {
                // Ціль уже вбита кимось іншим — повертаємось без бою
                TurnMarchBack(march, attackerArmy);
                return;
            }

            var defenderArmy = _armyBuilder.BuildArmy(monster.Type, monster.Level);
            var garrison = await _garrisonRepository.GetByIdAsync(march.GarrisonId, cancellationToken); 
            var village = garrison is null
                ? null
                : await _villageRepository.GetByIdAsync(garrison.VillageId, cancellationToken);

            var attackerBonus = village is null
                ? 1.0
                : await _effectResolver.GetMultiplierAsync(village.PlayerId, EffectTarget.Attack, DateTime.UtcNow, cancellationToken);

            var result = _combat.Resolve(attackerArmy, defenderArmy, terrain, attackerBonus);

            // Вільна місткість Госпіталю = сума рівнів × місткість на рівень − уже поранені
            var woundedCapacity = CalculateWoundedCapacity(village, garrison);

            var split = _casualties.Split(result.AttackerLosses, woundedCapacity);

            march.ApplyLosses(result.AttackerLosses);
            garrison?.AdmitWounded(split.Wounded);

            if (result.AttackerWon)
            {
                // Монстр знищений — прибираємо з карти
                _monsterRepository.Remove(monster);

                var cell = await _mapRepository.GetByOccupantAsync(MapOccupantType.Monster, monster.Id, cancellationToken);
                if (cell is not null)
                    _mapRepository.Remove(cell);

                var rewards = _armyBuilder.BuildRewards(monster.Type, monster.Level);
                village?.GrantResources(rewards);
                
            }

            var report = new BattleReport(
                Guid.NewGuid(),
                village?.PlayerId ?? Guid.Empty,
                march.Id,
                march.TargetX, march.TargetY, terrain,
                $"{monster.Type} (lvl {monster.Level})", monster.Level,
                result.AttackerWon, result.AttackerPower, result.DefenderPower, DateTime.UtcNow);

            foreach (var (unitType, sent) in attackerArmy)
            {
                report.AddLine(
                    unitType,
                    sent,
                    split.Wounded.GetValueOrDefault(unitType),
                    split.Recoverable.GetValueOrDefault(unitType),
                    split.Dead.GetValueOrDefault(unitType));
            }

            await _battleReportRepository.AddAsync(report, cancellationToken);

            // Відновлюваних кладемо окремим стеком: у кожного бою свій дедлайн викупу
            if (garrison is not null && split.Recoverable.Count > 0)
            {
                var expiresAt = DateTime.UtcNow.AddHours(_combatConfig.RecoveryWindowHours);
                garrison.AddRecoverable(split.Recoverable, report.Id, expiresAt);
            }

            march.RecordBattle(village?.PlayerId ?? Guid.Empty, report.Id, result.AttackerWon, report.TargetName);

            _logger.LogInformation(
               "Battle at ({X},{Y}) on {Terrain}: attacker {Outcome} ({AttackerPower:F0} vs {DefenderPower:F0}); " +
               "losses — wounded {Wounded}, recoverable {Recoverable}, dead {Dead}",
               march.TargetX, march.TargetY, terrain,
               result.AttackerWon ? "won" : "lost",
               result.AttackerPower, result.DefenderPower,
               split.Wounded.Values.Sum(), split.Recoverable.Values.Sum(), split.Dead.Values.Sum());

            TurnMarchBack(march, march.GetUnits());
        }

        /// <summary>Розвертає похід додому (або завершує, якщо армія загинула).</summary>
        private void TurnMarchBack(March march, IReadOnlyDictionary<string, int> survivors)
        {
            if (survivors.Count == 0 || survivors.Values.All(c => c <= 0))
            {
                // Уся армія загинула — повертатись нікому
                march.TurnBack(TimeSpan.Zero, DateTime.UtcNow);
                march.Complete();
                return;
            }

            var backDuration = _calculator.CalculateDuration(ServerId, march.TargetX, march.TargetY, march.OriginX, march.OriginY, survivors);

            march.TurnBack(backDuration, DateTime.UtcNow);
        }
        /// <summary>
        /// Вільних місць у Госпіталі: сума (рівень × місткість на рівень) мінус уже поранені.
        /// Немає Госпіталю — немає поранених, усі втрати безповоротні.
        /// </summary>
        private int CalculateWoundedCapacity(Village? village, Garrison? garrison)
        {
            if (village is null || garrison is null)
                return 0;

            var buildingConfigs = _gameConfig.Buildings.ToDictionary(b => b.Key, b => b);

            var total = village.Buildings
                .Where(b => !b.IsUnderConstruction)
                .Sum(b => buildingConfigs.TryGetValue(b.Type, out var cfg)
                    ? cfg.WoundedCapacityPerLevel * b.Level.Value
                    : 0);

            return Math.Max(0, total - garrison.WoundedCount);
        }

    }
}