using EmpireIdle.Application.Common.Services;
using EmpireIdle.Application.Interfaces;
using EmpireIdle.Domain.Entities;
using EmpireIdle.Domain.Enums;
using EmpireIdle.Domain.Services;
using EmpireIdle.Domain.Services.Config;
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
        private readonly IRandomSource _random;
        private readonly IGameNotifier _notifier;
        private readonly IServerRepository _serverRepository;
        private readonly TimeProvider _timeProvider;
        private readonly GameCatalog _catalog;
        private readonly CombatConfig _combatConfig;
        private readonly MonsterArmyBuilder _armyBuilder;
        private readonly TerrainGenerator _terrain;
        private readonly MarchCalculator _calculator;
        private readonly EffectResolver _effectResolver;
        private readonly BattleResolver _resolver;
        private readonly DefenceLossAllocator _lossAllocator;
        private readonly CasualtySplitter _casualties;
        private readonly WorldGeometry _geometry;
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
            IRandomSource random,
            IGameNotifier notifier,
            IServerRepository serverRepository,
        GameCatalog catalog,
            TimeProvider timeProvider,
            MonsterArmyBuilder armyBuilder,
            TerrainGenerator terrain,
            MarchCalculator calculator,
            EffectResolver effectResolver,
            BattleResolver resolver,
            DefenceLossAllocator lossAllocator,
            CasualtySplitter casualties,
            WorldGeometry geometry,
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
            _random = random;
            _notifier = notifier;
            _serverRepository = serverRepository;
            _armyBuilder = armyBuilder;
            _terrain = terrain;
            _calculator = calculator;
            _timeProvider = timeProvider;
            _effectResolver = effectResolver;
            _lossAllocator = lossAllocator;
            _casualties = casualties;
            _logger = logger;
            _catalog = catalog;
            _resolver = resolver;
            _geometry = geometry;
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

                await UnloadCargoAsync(march, garrison, now, cancellationToken);

                march.Complete(now);
            }

            await _unitOfWork.SaveChangesAsync(cancellationToken);
        }

        /// <summary>Проводить бій на місці прибуття армії.</summary>
        private async Task ResolveBattleAsync(March march, DateTime utcNow, CancellationToken cancellationToken)
        {
            var attackerArmy = march.GetUnits();
            var terrain = _terrain.GetTerrainType(march.ServerId, march.TargetX, march.TargetY);

            if (march.TargetType == MarchTargetType.Village)
            {
                await ResolveVillageBattleAsync(march, attackerArmy, terrain, utcNow, cancellationToken);
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
            var seed = _random.Next(int.MaxValue);

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

                // Здобич не з'являється в момент перемоги: вона їде з армією
                // й лягає на склад лише по прибутті, у межах вантажопідйомності
                var rewards = _armyBuilder.BuildRewards(monster.Type, monster.Level);
                var carried = LimitToCarryCapacity(
                    rewards.ToDictionary(r => r.Resource, r => r.Amount),
                    march.GetUnits());

                march.LoadCargo(carried, utcNow);
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

        /// <summary>
        /// Бій за село. Захисник — гарнізон господаря разом із підкріпленнями
        /// клану; втрати кожної сторони розкидаються по власниках, і поранені
        /// йдуть у госпіталь того, чиї це юніти, а не того, хто оборонявся.
        ///
        /// Здобич тут не рахується — вона наступним кроком, разом
        /// із захищеним запасом.
        /// </summary>
        private async Task ResolveVillageBattleAsync(March march, Dictionary<string, int> attackerArmy,
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
                TurnMarchBack(march, attackerArmy, utcNow);
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

            var attackerWoundedCapacity = CalculateWoundedCapacity(attackerVillage, attackerGarrison);

            var outcome = _resolver.Resolve(attackerArmy, defenderArmy, terrain, seed,
                attackerBonus, defenderBonus, attackerWoundedCapacity);

            var result = outcome.Battle;

            march.ApplyLosses(result.AttackerLosses, utcNow);
            attackerGarrison.AdmitWounded(outcome.AttackerCasualties.Wounded, utcNow);

            var defenderLosses = _lossAllocator.Allocate(defence, result.DefenderLosses);

            targetGarrison.ApplyDefenceLosses(defenderLosses, utcNow);

            await AdmitDefenderWoundedAsync(defenderLosses, seed, utcNow, cancellationToken);

            if (result.AttackerWon)
                await PlunderAsync(march, targetVillage, utcNow, cancellationToken);

            await WriteBattleReportsAsync(march, attackerVillage, attackerGarrison,
                targetVillage, targetGarrison, attackerArmy, defence, outcome, defenderLosses,
                terrain, seed, utcNow, cancellationToken);

            TurnMarchBack(march, march.GetUnits(), utcNow);

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
            var capacity = CalculateCarryCapacity(march.GetUnits());

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
        /// Розводить поранених захисника по госпіталях власників: свої —
        /// господарю, підкріплення — тому, хто їх прислав. Кожен платить
        /// за своїх, і чужий госпіталь чужими не забивається.
        /// </summary>
        private async Task AdmitDefenderWoundedAsync(IReadOnlyList<StackLoss> losses, int seed, DateTime utcNow, CancellationToken cancellationToken)
        {
            foreach (var group in losses.GroupBy(l => l.OwnerPlayerId))
            {
                // Поранені господаря лягли в WriteBattleReportsAsync
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

                var ownerCapacity = CalculateWoundedCapacity(ownerVillage, ownerGarrison);
                var ownerSplit = _casualties.Split(byType, ownerCapacity, seed ^ 0x1B873593);

                ownerGarrison.AdmitWounded(ownerSplit.Wounded, utcNow);
            }
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
        /// Розвантажує здобич на склад. Надлишок понад кап згорає: везти
        /// більше, ніж вміщає сховище, гравець може, зберегти — ні.
        /// </summary>
        private async Task UnloadCargoAsync(March march, Garrison garrison, DateTime utcNow,
            CancellationToken cancellationToken)
        {
            var cargo = march.GetCargo();

            if (cargo.Count == 0)
                return;

            var village = await _villageRepository.GetByIdAsync(garrison.VillageId, cancellationToken)
                ?? throw new InvalidOperationException($"Village {garrison.VillageId} not found for garrison {garrison.Id}.");

            var stored = 0;

            foreach (var (resourceType, amount) in cargo)
                stored += village.GrantResource(resourceType, amount, _catalog.Buildings, utcNow);

            _logger.LogInformation("March {MarchId} unloaded {Stored} of {Carried} carried resources.",
                march.Id, stored, cargo.Values.Sum());
        }

        /// <summary>
        /// Скільки армія здатна винести: сума CarryCapacity по вцілілих.
        /// Саме по вцілілих — інакше вигідно вести гарматне м'ясо заради місця.
        /// </summary>
        private int CalculateCarryCapacity(IReadOnlyDictionary<string, int> survivors)
            => survivors.Sum(pair => _catalog.Units.TryGetValue(pair.Key, out var config)
                ? (int)(config.Stats.GetValueOrDefault("CarryCapacity", 0) * pair.Value)
                : 0);

        /// <summary>
        /// Обрізає здобич до вантажопідйомності, пропорційно по ресурсах.
        /// Залишок від округлення дістається найбільшій позиції — інакше
        /// сума частин розійшлася б із лімітом.
        /// </summary>
        private Dictionary<string, int> LimitToCarryCapacity(
            IReadOnlyDictionary<string, int> loot, IReadOnlyDictionary<string, int> survivors)
        {
            var total = loot.Values.Sum();
            var capacity = CalculateCarryCapacity(survivors);

            if (total <= capacity)
                return loot.ToDictionary(pair => pair.Key, pair => pair.Value);

            if (capacity <= 0)
                return [];

            var limited = loot.ToDictionary(
                pair => pair.Key,
                pair => (int)Math.Floor((double)pair.Value * capacity / total));

            var shortfall = capacity - limited.Values.Sum();

            if (shortfall > 0)
            {
                var biggest = loot.OrderByDescending(pair => pair.Value).First().Key;
                limited[biggest] += shortfall;
            }

            return limited.Where(pair => pair.Value > 0).ToDictionary(pair => pair.Key, pair => pair.Value);
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

        /// <summary>
        /// Пише звіти обом сторонам. Кожен бачить бій зі свого боку:
        /// нападник — що втратив зі свого маршу, захисник — що втратив
        /// власний гарнізон. Підкріплення в звіт господаря не потрапляють:
        /// це чужі юніти, і в рядках вони виглядали б як його власні.
        /// </summary>
        private async Task WriteBattleReportsAsync(
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

            var hostCapacity = CalculateWoundedCapacity(defenderVillage, defenderGarrison);
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
