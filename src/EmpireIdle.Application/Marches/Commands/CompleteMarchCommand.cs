using EmpireIdle.Application.Common.Services;
using EmpireIdle.Application.Interfaces;
using EmpireIdle.Domain.Entities;
using EmpireIdle.Domain.Services;
using MediatR;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace EmpireIdle.Application.Marches.Commands
{
    /// <summary>Обробляє один похід, чий час прибуття настав.</summary>
    public record CompleteMarchCommand(Guid MarchId) : IRequest;

    public sealed class CompleteMarchCommandHandler : IRequestHandler<CompleteMarchCommand>
    {
        private readonly IMarchRepository _marchRepository;
        private readonly IGarrisonRepository _garrisonRepository;
        private readonly IUnitOfWork _unitOfWork;
        private readonly IMapRepository _mapRepository;
        private readonly IMonsterRepository _monsterRepository;
        private readonly IVillageRepository _villageRepository;
        private readonly IBattleReportRepository _battleReportRepository;
        private readonly IClanRepository _clanRepository;
        private readonly TimeProvider _timeProvider;
        private readonly GameCatalog _catalog;
        private readonly CombatConfig _combatConfig;
        private readonly MonsterArmyBuilder _armyBuilder;
        private readonly TerrainGenerator _terrain;
        private readonly MarchCalculator _calculator;
        private readonly EffectResolver _effectResolver;
        private readonly BattleResolver _resolver;
        private readonly ILogger<CompleteMarchCommandHandler> _logger;

        public CompleteMarchCommandHandler(
            IMarchRepository marchRepository,
            IGarrisonRepository garrisonRepository,
            IUnitOfWork unitOfWork,
            IMapRepository mapRepository,
            IMonsterRepository monsterRepository,
            IVillageRepository villageRepository,
            IBattleReportRepository battleReportRepository,
            IClanRepository clanRepository,
            GameCatalog catalog,
            TimeProvider timeProvider,
            MonsterArmyBuilder armyBuilder,
            TerrainGenerator terrain,
            MarchCalculator calculator,
            EffectResolver effectResolver,
            BattleResolver resolver,
            ILogger<CompleteMarchCommandHandler> logger)
        {
            _marchRepository = marchRepository;
            _garrisonRepository = garrisonRepository;
            _unitOfWork = unitOfWork;
            _mapRepository = mapRepository;
            _monsterRepository = monsterRepository;
            _villageRepository = villageRepository;
            _battleReportRepository = battleReportRepository;
            _clanRepository = clanRepository;
            _armyBuilder = armyBuilder;
            _terrain = terrain;
            _calculator = calculator;
            _timeProvider = timeProvider;
            _effectResolver = effectResolver;
            _logger = logger;
            _catalog = catalog;
            _resolver = resolver;
            _combatConfig = _catalog.Config.Combat;
        }

        public async Task Handle(CompleteMarchCommand request, CancellationToken cancellationToken)
        {
            var now = _timeProvider.GetUtcNow().UtcDateTime;

            var march = await _marchRepository.GetByIdAsync(request.MarchId, cancellationToken);

            // Марш міг обробити паралельний прогін сканера
            if (march is null || march.State == MarchState.Completed)
                return;

            if (march.State == MarchState.Outbound)
            {
                if (march.Intent == MarchIntent.Reinforce)
                    await DeliverReinforcementsAsync(march, now, cancellationToken);
                else
                    await ResolveBattleAsync(march, now, cancellationToken);
            }
            else if (march.State == MarchState.Returning)
            {
                var garrison = await _garrisonRepository.GetByIdAsync(march.GarrisonId, cancellationToken)
                    ?? throw new InvalidOperationException(
                        $"Garrison {march.GarrisonId} not found for march {march.Id}.");

                var survivors = march.GetUnits();
                if (survivors.Count > 0)
                    garrison.ReceiveUnits(survivors, now);

                march.Complete(now);
            }

            await _unitOfWork.SaveChangesAsync(cancellationToken);
        }

        /// <summary>Проводить бій на місці прибуття армії.</summary>
        private async Task ResolveBattleAsync(March march, DateTime utcNow, CancellationToken cancellationToken)
        {
            var attackerArmy = march.GetUnits();
            var terrain = _terrain.GetTerrainType(march.ServerId, march.TargetX, march.TargetY);

            if (march.TargetType != MarchTargetType.Monster)
            {
                // PvP — окрема фаза; поки армія просто розвертається
                TurnMarchBack(march, attackerArmy, utcNow);
                return;
            }

            var monster = await _monsterRepository.GetByIdAsync(march.TargetId, cancellationToken);
            if (monster is null)
            {
                // Ціль уже вбита кимось іншим — повертаємось без бою
                TurnMarchBack(march, attackerArmy, utcNow);
                return;
            }

            var defenderArmy = _armyBuilder.BuildArmy(monster.Type, monster.Level);

            var garrison = await _garrisonRepository.GetByIdAsync(march.GarrisonId, cancellationToken)
                 ?? throw new InvalidOperationException($"Garrison {march.GarrisonId} not found for march {march.Id}.");

            var village = await _villageRepository.GetByIdAsync(garrison.VillageId, cancellationToken)
                ?? throw new InvalidOperationException($"Village {garrison.VillageId} not found for garrison {garrison.Id}.");

            var attackerBonus = await _effectResolver.GetMultiplierAsync(village.PlayerId, EffectTarget.Attack, utcNow, cancellationToken);

            // Сід фіксуємо до бою: він іде і в розрахунок, і у звіт
            var seed = Random.Shared.Next();

            // Вільна місткість Госпіталю = сума рівнів × місткість на рівень − уже поранені
            var woundedCapacity = CalculateWoundedCapacity(village, garrison);

            // Монстр стін не має, тому бонус захисника лишається нейтральним
            var outcome = _resolver.Resolve(attackerArmy, defenderArmy, terrain, seed,
                attackerBonus, defenderBonus: 1.0, woundedCapacity);

            var result = outcome.Battle;
            var split = outcome.AttackerCasualties;

            march.ApplyLosses(result.AttackerLosses, utcNow);
            garrison.AdmitWounded(split.Wounded, utcNow);

            if (result.AttackerWon)
            {
                // Монстр знищений — прибираємо з карти
                _monsterRepository.Remove(monster);

                var cell = await _mapRepository.GetByOccupantAsync(MapOccupantType.Monster, monster.Id, cancellationToken);
                if (cell is not null)
                    _mapRepository.Remove(cell);

                var rewards = _armyBuilder.BuildRewards(monster.Type, monster.Level);
                village.GrantResources(rewards, utcNow);
            }

            var report = new BattleReport(
                Guid.NewGuid(),
                village.PlayerId,
                march.Id,
                march.TargetX, march.TargetY, terrain,
                $"{monster.Type} (lvl {monster.Level})", monster.Level,
                result.AttackerWon, result.AttackerPower, result.DefenderPower, seed, utcNow);

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
            if (split.Recoverable.Count > 0)
            {
                var expiresAt = utcNow.AddHours(_combatConfig.RecoveryWindowHours);
                garrison.AddRecoverable(split.Recoverable, report.Id, expiresAt, utcNow);
            }

            march.RecordBattle(village.PlayerId, report.Id, result.AttackerWon, report.TargetName, utcNow);

            _logger.LogInformation(
               "Battle at ({X},{Y}) on {Terrain}: attacker {Outcome} ({AttackerPower:F0} vs {DefenderPower:F0}); " +
               "losses — wounded {Wounded}, recoverable {Recoverable}, dead {Dead}",
               march.TargetX, march.TargetY, terrain,
               result.AttackerWon ? "won" : "lost",
               result.AttackerPower, result.DefenderPower,
               split.Wounded.Values.Sum(), split.Recoverable.Values.Sum(), split.Dead.Values.Sum());

            TurnMarchBack(march, march.GetUnits(), utcNow);
        }

        /// <summary>
        /// Ставить армію в гарнізон союзника.
        ///
        /// Якщо за час дороги союзник вийшов із клану, село зникло або
        /// посольство переповнилось — армія розвертається. Кидати виняток
        /// тут не можна: це прогін сканера, а не запит гравця, і падіння
        /// заблокувало б решту маршів у пакеті.
        /// </summary>
        private async Task DeliverReinforcementsAsync(March march, DateTime utcNow, CancellationToken cancellationToken)
        {
            var units = march.GetUnits();

            var ownerGarrison = await _garrisonRepository.GetByIdAsync(march.GarrisonId, cancellationToken)
                ?? throw new InvalidOperationException($"Garrison {march.GarrisonId} not found for march {march.Id}.");

            var ownerVillage = await _villageRepository.GetByIdAsync(ownerGarrison.VillageId, cancellationToken)
                ?? throw new InvalidOperationException($"Village {ownerGarrison.VillageId} not found for garrison {ownerGarrison.Id}.");

            var targetVillage = await _villageRepository.GetByIdAsync(march.TargetId, cancellationToken);
            var targetGarrison = targetVillage is null
                ? null
                : await _garrisonRepository.GetByVillageIdAsync(targetVillage.Id, cancellationToken);

            if (targetVillage is null || targetGarrison is null)
            {
                TurnMarchBack(march, units, utcNow);
                return;
            }

            var ownerClan = await _clanRepository.GetClanIdByMemberAsync(ownerVillage.PlayerId, cancellationToken);
            var targetClan = await _clanRepository.GetClanIdByMemberAsync(targetVillage.PlayerId, cancellationToken);

            if (ownerClan is null || ownerClan != targetClan)
            {
                _logger.LogInformation("March {MarchId} turned back: no longer clanmates", march.Id);

                TurnMarchBack(march, units, utcNow);
                return;
            }

            var capacity = targetVillage.ReinforcementCapacity(_catalog.Buildings);
            var incoming = units.Values.Sum();

            if (targetGarrison.ReinforcementCount + incoming > capacity)
            {
                _logger.LogInformation(
                    "March {MarchId} turned back: embassy at village {VillageId} has no room for {Incoming} units",
                    march.Id, targetVillage.Id, incoming);

                TurnMarchBack(march, units, utcNow);
                return;
            }

            targetGarrison.AddReinforcements(ownerVillage.PlayerId, ownerGarrison.Id, units, capacity, utcNow);
            march.Delivered(utcNow);

            _logger.LogInformation("March {MarchId} delivered {Incoming} units to village {VillageId}",
                march.Id, incoming, targetVillage.Id);
        }

        /// <summary>Розвертає похід додому (або завершує, якщо армія загинула).</summary>
        private void TurnMarchBack(March march, IReadOnlyDictionary<string, int> survivors, DateTime utcNow)
        {
            if (survivors.Count == 0 || survivors.Values.All(c => c <= 0))
            {
                // Уся армія загинула — повертатись нікому
                march.TurnBack(TimeSpan.Zero, utcNow);
                march.Complete(utcNow);
                return;
            }

            var backDuration = _calculator.CalculateDuration(
                march.ServerId, march.TargetX, march.TargetY, march.OriginX, march.OriginY, survivors);

            march.TurnBack(backDuration, utcNow);
        }

        /// <summary>
        /// Вільних місць у Госпіталі: сума (рівень × місткість на рівень) мінус уже поранені.
        /// Немає Госпіталю — немає поранених, усі втрати безповоротні.
        /// </summary>
        private int CalculateWoundedCapacity(Village village, Garrison garrison)
        {
            if (village is null || garrison is null)
                return 0;

            var buildingConfigs = _catalog.Buildings;

            var total = village.Buildings
                .Where(b => !b.IsUnderConstruction)
                .Sum(b => buildingConfigs.TryGetValue(b.Type, out var cfg)
                    ? cfg.WoundedCapacityPerLevel * b.Level.Value
                    : 0);

            return Math.Max(0, total - garrison.WoundedCount);
        }
    }
}
