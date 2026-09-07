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
    /// Бій із монстром — PvE-сценарій прибуття.
    ///
    /// На відміну від бою за село, тут одна сторона з власником: монстр
    /// нічого не втрачає, він або зникає з карти, або лишається. Тому
    /// й наслідки простіші — один звіт, один гарнізон, один кошик викупу.
    /// </summary>
    public sealed class MonsterBattleService
    {
        private readonly IMonsterRepository _monsterRepository;
        private readonly IMapRepository _mapRepository;
        private readonly IGarrisonRepository _garrisonRepository;
        private readonly IVillageRepository _villageRepository;
        private readonly IRandomSource _random;
        private readonly MonsterArmyBuilder _armyBuilder;
        private readonly BattleResolver _resolver;
        private readonly EffectResolver _effectResolver;
        private readonly MarchLogistics _logistics;
        private readonly BattleAftermath _aftermath;
        private readonly ILogger<MonsterBattleService> _logger;

        public MonsterBattleService(
            IMonsterRepository monsterRepository,
            IMapRepository mapRepository,
            IGarrisonRepository garrisonRepository,
            IVillageRepository villageRepository,
            IRandomSource random,
            MonsterArmyBuilder armyBuilder,
            BattleResolver resolver,
            EffectResolver effectResolver,
            MarchLogistics logistics,
            BattleAftermath aftermath,
            ILogger<MonsterBattleService> logger)
        {
            _monsterRepository = monsterRepository;
            _mapRepository = mapRepository;
            _garrisonRepository = garrisonRepository;
            _villageRepository = villageRepository;
            _random = random;
            _armyBuilder = armyBuilder;
            _resolver = resolver;
            _effectResolver = effectResolver;
            _logistics = logistics;
            _aftermath = aftermath;
            _logger = logger;
        }

        /// <summary>
        /// Проводить бій із монстром на клітині прибуття.
        /// Ціль могла загинути від чужої руки — тоді армія просто вертається.
        /// </summary>
        public async Task ResolveAsync(March march, Dictionary<string, int> attackerArmy,
            string terrain, DateTime utcNow, CancellationToken cancellationToken)
        {
            var monster = await _monsterRepository.GetByIdAsync(march.TargetId, cancellationToken);

            if (monster is null)
            {
                // Ціль уже вбита кимось іншим — повертаємось без бою
                _logistics.TurnMarchBack(march, attackerArmy, utcNow);
                return;
            }

            var defenderArmy = _armyBuilder.BuildArmy(monster.Type, monster.Level);

            var garrison = await _garrisonRepository.GetByIdAsync(march.GarrisonId, cancellationToken)
                 ?? throw new InvalidOperationException($"Garrison {march.GarrisonId} not found for march {march.Id}.");

            var village = await _villageRepository.GetByIdAsync(garrison.VillageId, cancellationToken)
                ?? throw new InvalidOperationException($"Village {garrison.VillageId} not found for garrison {garrison.Id}.");

            var attackerBonus = await _effectResolver.GetMultiplierAsync(
                village.PlayerId, EffectTarget.Attack, utcNow, cancellationToken);

            // Сід фіксуємо до бою: він іде і в розрахунок, і у звіт
            var seed = _random.Next(int.MaxValue);

            var woundedCapacity = _logistics.CalculateWoundedCapacity(village, garrison);

            // Монстр стін не має, тому бонус захисника лишається нейтральним
            var outcome = _resolver.Resolve(attackerArmy, defenderArmy, terrain, seed,
                attackerBonus, defenderBonus: 1.0, woundedCapacity);

            var result = outcome.Battle;
            var split = outcome.AttackerCasualties;

            march.ApplyLosses(result.AttackerLosses, utcNow);
            garrison.AdmitWounded(split.Wounded, utcNow);

            if (result.AttackerWon)
                await TakeSpoilsAsync(march, monster, utcNow, cancellationToken);

            await _aftermath.RecordAttackerAsync(march, village, garrison,
                $"{monster.Type} (lvl {monster.Level})", monster.Level,
                attackerArmy, outcome, terrain, seed, utcNow, cancellationToken);

            _logger.LogInformation(
               "Battle at ({X},{Y}) on {Terrain}: attacker {Outcome} ({AttackerPower:F0} vs {DefenderPower:F0}); " +
               "losses — wounded {Wounded}, recoverable {Recoverable}, dead {Dead}",
               march.TargetX, march.TargetY, terrain,
               result.AttackerWon ? "won" : "lost",
               result.AttackerPower, result.DefenderPower,
               split.Wounded.Values.Sum(), split.Recoverable.Values.Sum(), split.Dead.Values.Sum());

            _logistics.TurnMarchBack(march, march.GetUnits(), utcNow);
        }

        /// <summary>
        /// Прибирає переможеного монстра з карти й вантажить нагороду.
        /// Здобич не з'являється в момент перемоги: вона їде з армією
        /// й лягає на склад лише по прибутті, у межах вантажопідйомності.
        /// </summary>
        private async Task TakeSpoilsAsync(March march, Monster monster, DateTime utcNow,
            CancellationToken cancellationToken)
        {
            _monsterRepository.Remove(monster);

            var cell = await _mapRepository.GetByOccupantAsync(MapOccupantType.Monster, monster.Id, cancellationToken);

            if (cell is not null)
                _mapRepository.Remove(cell);

            var rewards = _armyBuilder.BuildRewards(monster.Type, monster.Level);

            var carried = _logistics.LimitToCarryCapacity(
                rewards.ToDictionary(r => r.Resource, r => r.Amount),
                march.GetUnits());

            march.LoadCargo(carried, utcNow);
        }
    }
}
