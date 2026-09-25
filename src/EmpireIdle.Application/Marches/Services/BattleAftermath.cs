using EmpireIdle.Application.Clans.Services;
using EmpireIdle.Application.Interfaces;
using EmpireIdle.Domain.Combat;
using EmpireIdle.Domain.Entities;
using EmpireIdle.Domain.Services;
using EmpireIdle.Domain.Services.Config;
using EmpireIdle.Domain.ValueObjects;
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
        private readonly IHeroRepository _heroRepository;
        private readonly IGameNotifier _notifier;
        private readonly CasualtySplitter _casualties;
        private readonly CombatConfig _combatConfig;
        private readonly MarchLogistics _logistics;
        private readonly VillageStatus _status;
        private readonly ReinforcementReturner _returner;
        private readonly ILogger<BattleAftermath> _logger;

        public BattleAftermath(
            IBattleReportRepository battleReportRepository,
            IGarrisonRepository garrisonRepository,
            IVillageRepository villageRepository,
            IHeroRepository heroRepository,
            IGameNotifier notifier,
            CasualtySplitter casualties,
            GameCatalog catalog,
            MarchLogistics logistics,
            VillageStatus status,
            ReinforcementReturner returner,
            ILogger<BattleAftermath> logger)
        {
            _battleReportRepository = battleReportRepository;
            _garrisonRepository = garrisonRepository;
            _villageRepository = villageRepository;
            _heroRepository = heroRepository;
            _notifier = notifier;
            _casualties = casualties;
            _combatConfig = catalog.Config.Combat;
            _logistics = logistics;
            _status = status;
            _returner = returner;
            _logger = logger;
        }

        /// <summary>Згортає стеки (тип+рівень) до типу — рядки звіту рівня не розрізняють.</summary>
        private static Dictionary<string, int> ByType(IReadOnlyDictionary<UnitStackKey, int> stacks)
            => stacks.GroupBy(p => p.Key.UnitType).ToDictionary(g => g.Key, g => g.Sum(p => p.Value));

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
            IReadOnlyDictionary<UnitStackKey, int> army,
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

            var armyByType = ByType(army);
            var woundedByType = ByType(split.Wounded);
            var recoverableByType = ByType(split.Recoverable);
            var deadByType = ByType(split.Dead);

            foreach (var (unitType, sent) in armyByType)
            {
                report.AddLine(
                    unitType,
                    sent,
                    woundedByType.GetValueOrDefault(unitType),
                    recoverableByType.GetValueOrDefault(unitType),
                    deadByType.GetValueOrDefault(unitType));
            }

            await _battleReportRepository.AddAsync(report, cancellationToken);

            // Відновлюваних кладемо окремим стеком: у кожного бою свій дедлайн викупу
            if (split.Recoverable.Count > 0)
                garrison.AddRecoverable(split.Recoverable, report.Id,
                    utcNow.AddHours(_combatConfig.RecoveryWindowHours), utcNow);

            // Провалена атака кладе героя в госпіталь. Ранить лише його:
            // марш веде рівно один герой, а вдома ростер бою не бачив
            if (!result.AttackerWon && march.HeroId is Guid heroId)
            {
                var hero = await _heroRepository.GetByIdAsync(heroId, cancellationToken);

                hero?.Wound(utcNow);
            }

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
                attackerVillage.Name, _status.MainBuildingLevel(attackerVillage),
                !result.AttackerWon, result.AttackerPower, result.DefenderPower, seed, utcNow);

            var hostStood = defence
                .Where(s => s.OwnerPlayerId is null)
                .GroupBy(s => new UnitStackKey(s.UnitType, s.Level))
                .ToDictionary(g => g.Key, g => g.Sum(s => s.Count));

            var hostLost = defenderLosses
                .Where(l => l.OwnerPlayerId is null)
                .GroupBy(l => new UnitStackKey(l.UnitType, l.Level))
                .ToDictionary(g => g.Key, g => g.Sum(l => l.Lost));

            var capacity = _logistics.CalculateWoundedCapacity(defenderVillage, defenderGarrison);
            var split = _casualties.Split(hostLost, capacity, WoundedSeed(seed));

            defenderGarrison.AdmitWounded(split.Wounded, utcNow);

            // Програна оборона ранить лідера гарнізону. Решта героїв стоїть
            // у резерві й у бою не була: інакше одна поразка клала б увесь зал
            if (result.AttackerWon)
            {
                var leader = await _heroRepository.GetLeaderAsync(
                    defenderGarrison.Id, defenderVillage.PlayerId, cancellationToken);

                leader?.Wound(utcNow);
            }

            var hostStoodByType = ByType(hostStood);
            var woundedByType = ByType(split.Wounded);
            var recoverableByType = ByType(split.Recoverable);
            var deadByType = ByType(split.Dead);

            foreach (var (unitType, stood) in hostStoodByType)
            {
                report.AddLine(
                    unitType,
                    stood,
                    woundedByType.GetValueOrDefault(unitType),
                    recoverableByType.GetValueOrDefault(unitType),
                    deadByType.GetValueOrDefault(unitType));
            }

            await _battleReportRepository.AddAsync(report, cancellationToken);

            if (split.Recoverable.Count > 0)
                defenderGarrison.AddRecoverable(split.Recoverable, report.Id,
                    utcNow.AddHours(_combatConfig.RecoveryWindowHours), utcNow);

            await _notifier.NotifyBattleFinishedAsync(defenderVillage.PlayerId, report.Id,
                !result.AttackerWon, attackerVillage.Name, cancellationToken);
        }

        /// <summary>
        /// Розводить поранених підкріплень по госпіталях їхніх власників
        /// і, після програної оборони, розпускає контингенти по домівках.
        ///
        /// Кожен платить за своїх, і чужий госпіталь чужими не забивається.
        /// Власного звіту союзники не отримують, а отже й кошика викупу —
        /// це свідоме спрощення: один бій інакше породжував би до двохсот звітів.
        /// </summary>
        /// <param name="defenceLost">
        /// Чи впала оборона. Тільки тоді лідери союзників ідуть у госпіталь,
        /// а вцілілі повертаються додому: виграний бій лишає контингент стояти.
        /// </param>
        public async Task AdmitAlliedWoundedAsync(IReadOnlyList<StackLoss> losses, Garrison hostGarrison,
            Guid hostOwnerId, bool defenceLost, int seed, DateTime utcNow, CancellationToken cancellationToken)
        {
            foreach (var group in losses.GroupBy(l => l.OwnerPlayerId))
            {
                // Поранені господаря лягли разом із його звітом
                if (group.Key is not { } ownerId)
                    continue;

                await AdmitOwnerWoundedAsync(ownerId, LostBy(group), seed, utcNow, cancellationToken);
            }

            await DisbandIfLostAsync(hostGarrison, hostOwnerId, defenceLost, utcNow, cancellationToken);
        }

        /// <summary>
        /// Бій за кланову споруду для її захисників: кожен, хто стояв у гарнізоні, отримує
        /// свій звіт і кошик викупу, поранені йдуть у його госпіталь. На відміну від села
        /// (де союзники звітів не отримують) тут господаря немає — без звіту про бій не знав би ніхто.
        /// Юнітів у гарнізоні споруди обмежено місткістю, тож і звітів — одиниці, не сотні.
        /// </summary>
        public async Task RecordStructureDefenceAsync(
            March march,
            ClanStructure structure,
            Garrison structureGarrison,
            Village attackerVillage,
            IReadOnlyList<DefenceStack> defence,
            IReadOnlyList<StackLoss> defenderLosses,
            BattleResult result,
            string terrain,
            int seed,
            DateTime utcNow,
            CancellationToken cancellationToken)
        {
            foreach (var ownerId in defence.Select(s => s.OwnerPlayerId).OfType<Guid>().Distinct())
            {
                var stood = defence
                    .Where(s => s.OwnerPlayerId == ownerId)
                    .GroupBy(s => new UnitStackKey(s.UnitType, s.Level))
                    .ToDictionary(g => g.Key, g => g.Sum(s => s.Count));

                var admitted = await AdmitOwnerWoundedAsync(ownerId,
                    LostBy(defenderLosses.Where(l => l.OwnerPlayerId == ownerId)), seed, utcNow, cancellationToken);

                var report = new BattleReport(
                    Guid.NewGuid(), ownerId, march.Id,
                    structure.X, structure.Y, terrain,
                    attackerVillage.Name, _status.MainBuildingLevel(attackerVillage),
                    !result.AttackerWon, result.AttackerPower, result.DefenderPower, seed, utcNow);

                var woundedByType = ByType(admitted?.Split.Wounded ?? new Dictionary<UnitStackKey, int>());
                var recoverableByType = ByType(admitted?.Split.Recoverable ?? new Dictionary<UnitStackKey, int>());
                var deadByType = ByType(admitted?.Split.Dead ?? new Dictionary<UnitStackKey, int>());

                foreach (var (unitType, count) in ByType(stood))
                {
                    report.AddLine(unitType, count,
                        woundedByType.GetValueOrDefault(unitType),
                        recoverableByType.GetValueOrDefault(unitType),
                        deadByType.GetValueOrDefault(unitType));
                }

                await _battleReportRepository.AddAsync(report, cancellationToken);

                if (admitted is { } home && home.Split.Recoverable.Count > 0)
                    home.Garrison.AddRecoverable(home.Split.Recoverable, report.Id,
                        utcNow.AddHours(_combatConfig.RecoveryWindowHours), utcNow);

                await _notifier.NotifyBattleFinishedAsync(ownerId, report.Id,
                    !result.AttackerWon, attackerVillage.Name, cancellationToken);
            }

            // Господаря в споруди немає — після поразки додому йдуть усі
            await DisbandIfLostAsync(structureGarrison, Guid.Empty, result.AttackerWon, utcNow, cancellationToken);
        }

        private static Dictionary<UnitStackKey, int> LostBy(IEnumerable<StackLoss> losses)
            => losses
                .GroupBy(l => new UnitStackKey(l.UnitType, l.Level))
                .ToDictionary(g => g.Key, g => g.Sum(l => l.Lost));

        /// <summary>Поранені союзника — в його госпіталь; null — дому немає, поранених нікуди класти.</summary>
        private async Task<(Garrison Garrison, CasualtySplit Split)?> AdmitOwnerWoundedAsync(Guid ownerId,
            Dictionary<UnitStackKey, int> lost, int seed, DateTime utcNow, CancellationToken cancellationToken)
        {
            // Гарнізон союзника стоїть за сотню клітин, але транзакція одна
            var ownerVillage = await _villageRepository.GetByPlayerIdAsync(ownerId, cancellationToken);
            var ownerGarrison = ownerVillage is null
                ? null
                : await _garrisonRepository.GetByVillageIdAsync(ownerVillage.Id, cancellationToken);

            if (ownerVillage is null || ownerGarrison is null)
            {
                _logger.LogWarning("Wounded reinforcements of {OwnerId} dropped: no home garrison.", ownerId);
                return null;
            }

            var capacity = _logistics.CalculateWoundedCapacity(ownerVillage, ownerGarrison);
            var split = _casualties.Split(lost, capacity, WoundedSeed(seed));

            ownerGarrison.AdmitWounded(split.Wounded, utcNow);

            return (ownerGarrison, split);
        }

        /// <summary>
        /// Після програної оборони лідери союзників ідуть у госпіталь, а контингенти — додому.
        /// Виграний бій лишає їх стояти.
        /// </summary>
        private async Task DisbandIfLostAsync(Garrison hostGarrison, Guid hostOwnerId, bool defenceLost,
            DateTime utcNow, CancellationToken cancellationToken)
        {
            if (!defenceLost)
                return;

            // Власників беремо з гарнізону, а не з утрат: контингент міг
            // вистояти без жодної втрати, а додому все одно йде
            var owners = hostGarrison.ReinforcementOwners().ToList();

            var stationed = await _heroRepository.GetByGarrisonAsync(hostGarrison.Id, cancellationToken);

            foreach (var leader in stationed.Where(h => h.IsLeader && h.PlayerId != hostOwnerId))
                leader.Wound(utcNow);

            foreach (var ownerId in owners.Concat(stationed.Select(h => h.PlayerId)).Distinct())
            {
                if (ownerId == hostOwnerId)
                    continue;

                await _returner.ReturnOwnerAsync(hostGarrison, ownerId, utcNow, cancellationToken);
            }
        }

        /// <summary>
        /// Окремий сід для розподілу поранених захисника: із тим самим
        /// сідом, що й бій, поділ був би прив'язаний до його результату.
        /// </summary>
        private static int WoundedSeed(int battleSeed) => battleSeed ^ 0x1B873593;
    }
}
