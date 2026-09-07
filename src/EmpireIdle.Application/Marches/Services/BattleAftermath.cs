using EmpireIdle.Application.Interfaces;
using EmpireIdle.Domain.Entities;
using EmpireIdle.Domain.Services;
using EmpireIdle.Domain.Services.Config;
using Microsoft.Extensions.Logging;

namespace EmpireIdle.Application.Marches.Services
{
    /// <summary>
    /// Наслідки бою: звіти, поранені, кошики викупу, сповіщення.
    ///
    /// Живе окремо від самих боїв, бо бій із монстром і бій за село
    /// рахуються по-різному, а записуються однаково. Тримати це в кожному
    /// сервісі означало б дві копії того самого — і другу забути правити.
    /// </summary>
    public sealed class BattleAftermath
    {
        private readonly IBattleReportRepository _battleReportRepository;
        private readonly IGarrisonRepository _garrisonRepository;
        private readonly IVillageRepository _villageRepository;
        private readonly IGameNotifier _notifier;
        private readonly CasualtySplitter _casualties;
        private readonly GameCatalog _catalog;
        private readonly CombatConfig _combatConfig;
        private readonly MarchLogistics _logistics;
        private readonly ILogger<BattleAftermath> _logger;

        public BattleAftermath(
            IBattleReportRepository battleReportRepository,
            IGarrisonRepository garrisonRepository,
            IVillageRepository villageRepository,
            IGameNotifier notifier,
            CasualtySplitter casualties,
            GameCatalog catalog,
            MarchLogistics logistics,
            ILogger<BattleAftermath> logger)
        {
            _battleReportRepository = battleReportRepository;
            _garrisonRepository = garrisonRepository;
            _villageRepository = villageRepository;
            _notifier = notifier;
            _casualties = casualties;
            _catalog = catalog;
            _combatConfig = catalog.Config.Combat;
            _logistics = logistics;
            _logger = logger;
        }

        /// <summary>
        /// Записує бій нападнику: звіт із рядками по типах, кошик викупу
        /// й позначку на марші. Однаково для монстра й для села — з боку
        /// нападника між ними немає різниці.
        /// </summary>
        /// <param name="targetName">Що бачитиме гравець у списку звітів.</param>
        /// <param name="targetLevel">Рівень монстра або ратуші захисника.</param>
        /// <returns>Id створеного звіту.</returns>
        public async Task<Guid> RecordAttackerAsync(
            March march,
            Village village,
            Garrison garrison,
            string targetName,
            int targetLevel,
            IReadOnlyDictionary<string, int> army,
            BattleOutcome outcome,
            string terrain,
            int seed,
            DateTime utcNow,
            CancellationToken cancellationToken)
        {
            var result = outcome.Battle;
            var split = outcome.AttackerCasualties;

            var report = new BattleReport(
                Guid.NewGuid(),
                village.PlayerId,
                march.Id,
                march.TargetX, march.TargetY, terrain,
                targetName, targetLevel,
                result.AttackerWon, result.AttackerPower, result.DefenderPower, seed, utcNow);

            foreach (var (unitType, sent) in army)
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
                garrison.AddRecoverable(split.Recoverable, report.Id,
                    utcNow.AddHours(_combatConfig.RecoveryWindowHours), utcNow);

            march.RecordBattle(village.PlayerId, report.Id, result.AttackerWon, targetName, utcNow);

            return report.Id;
        }

        /// <summary>
        /// Записує бій захиснику: його власні втрати, звіт і сповіщення.
        ///
        /// Підкріплення в звіт не потрапляють — це чужі юніти, і в рядках
        /// вони виглядали б як його власні. Координати теж власні: бій ішов
        /// у нього вдома, а «ціллю» виступає нападник.
        /// </summary>
        public async Task RecordDefenderAsync(
            March march,
            Village defenderVillage,
            Garrison defenderGarrison,
            Village attackerVillage,
            IReadOnlyList<DefenceStack> defence,
            IReadOnlyList<StackLoss> defenderLosses,
            BattleResult result,
            string terrain,
            int seed,
            DateTime utcNow,
            CancellationToken cancellationToken)
        {
            var report = new BattleReport(
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

            var capacity = _logistics.CalculateWoundedCapacity(defenderVillage, defenderGarrison);
            var split = _casualties.Split(hostLost, capacity, WoundedSeed(seed));

            defenderGarrison.AdmitWounded(split.Wounded, utcNow);

            foreach (var (unitType, stood) in hostStood)
            {
                report.AddLine(
                    unitType,
                    stood,
                    split.Wounded.GetValueOrDefault(unitType),
                    split.Recoverable.GetValueOrDefault(unitType),
                    split.Dead.GetValueOrDefault(unitType));
            }

            await _battleReportRepository.AddAsync(report, cancellationToken);

            if (split.Recoverable.Count > 0)
                defenderGarrison.AddRecoverable(split.Recoverable, report.Id,
                    utcNow.AddHours(_combatConfig.RecoveryWindowHours), utcNow);

            await _notifier.NotifyBattleFinishedAsync(defenderVillage.PlayerId, report.Id,
                !result.AttackerWon, attackerVillage.Name, cancellationToken);
        }

        /// <summary>
        /// Розводить поранених підкріплень по госпіталях їхніх власників.
        /// Кожен платить за своїх, і чужий госпіталь чужими не забивається.
        ///
        /// Власного звіту союзники не отримують, а отже й кошика викупу —
        /// це свідоме спрощення: один бій інакше породжував би до двохсот звітів.
        /// </summary>
        public async Task AdmitAlliedWoundedAsync(IReadOnlyList<StackLoss> losses, int seed, DateTime utcNow,
            CancellationToken cancellationToken)
        {
            foreach (var group in losses.GroupBy(l => l.OwnerPlayerId))
            {
                // Поранені господаря лягли разом із його звітом
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
                {
                    _logger.LogWarning("Wounded reinforcements of {OwnerId} dropped: no home garrison.", ownerId);
                    continue;
                }

                var capacity = _logistics.CalculateWoundedCapacity(ownerVillage, ownerGarrison);
                var split = _casualties.Split(byType, capacity, WoundedSeed(seed));

                ownerGarrison.AdmitWounded(split.Wounded, utcNow);
            }
        }

        /// <summary>
        /// Окремий сід для розподілу поранених захисника: із тим самим
        /// сідом, що й бій, поділ був би прив'язаний до його результату.
        /// </summary>
        private static int WoundedSeed(int battleSeed) => battleSeed ^ 0x1B873593;
    }
}
