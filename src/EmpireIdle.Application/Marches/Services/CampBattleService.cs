using EmpireIdle.Application.Common.Services;
using EmpireIdle.Application.Interfaces;
using EmpireIdle.Application.Territory.Services;
using EmpireIdle.Domain.Combat;
using EmpireIdle.Domain.Entities;
using EmpireIdle.Domain.Enums;
using EmpireIdle.Domain.Services;
using EmpireIdle.Domain.ValueObjects;
using Microsoft.Extensions.Logging;

namespace EmpireIdle.Application.Marches.Services
{
    /// <summary>
    /// Бій за табір (§2.5). Захисник — армія табору під своїм героєм; стін у полі
    /// немає, решта правил — як у захисника: територія клану за клітинкою табору,
    /// поранені — у госпіталь власника. Грабувати нічого: табір здобичі не везе.
    /// Програний табір знімається й відступає додому маршем, вистояв — стоїть далі.
    /// </summary>
    public sealed class CampBattleService
    {
        private readonly IGarrisonRepository _garrisonRepository;
        private readonly IVillageRepository _villageRepository;
        private readonly IMarchRepository _marchRepository;
        private readonly IHeroRepository _heroRepository;
        private readonly IRandomSource _random;
        private readonly BattleResolver _resolver;
        private readonly DefenceLossAllocator _lossAllocator;
        private readonly EffectResolver _effectResolver;
        private readonly MarchLogistics _logistics;
        private readonly BattleAftermath _aftermath;
        private readonly VillageStatus _status;
        private readonly HeroCombatModifiers _heroModifiers;
        private readonly TerritoryBonus _territoryBonus;
        private readonly CampHomecoming _campHomecoming;
        private readonly ILogger<CampBattleService> _logger;

        public CampBattleService(
            IGarrisonRepository garrisonRepository,
            IVillageRepository villageRepository,
            IMarchRepository marchRepository,
            IHeroRepository heroRepository,
            IRandomSource random,
            BattleResolver resolver,
            DefenceLossAllocator lossAllocator,
            EffectResolver effectResolver,
            MarchLogistics logistics,
            BattleAftermath aftermath,
            VillageStatus status,
            HeroCombatModifiers heroModifiers,
            TerritoryBonus territoryBonus,
            CampHomecoming campHomecoming,
            ILogger<CampBattleService> logger)
        {
            _garrisonRepository = garrisonRepository;
            _villageRepository = villageRepository;
            _marchRepository = marchRepository;
            _heroRepository = heroRepository;
            _random = random;
            _resolver = resolver;
            _lossAllocator = lossAllocator;
            _effectResolver = effectResolver;
            _logistics = logistics;
            _aftermath = aftermath;
            _status = status;
            _heroModifiers = heroModifiers;
            _territoryBonus = territoryBonus;
            _campHomecoming = campHomecoming;
            _logger = logger;
        }

        public async Task ResolveAsync(March march, Dictionary<UnitStackKey, int> attackerArmy,
            string terrain, DateTime utcNow, CancellationToken cancellationToken)
        {
            var attackerGarrison = await _garrisonRepository.GetByIdAsync(march.GarrisonId, cancellationToken)
                ?? throw new InvalidOperationException($"Garrison {march.GarrisonId} not found for march {march.Id}.");

            var attackerVillage = await _villageRepository.GetByIdAsync(attackerGarrison.VillageId, cancellationToken)
                ?? throw new InvalidOperationException($"Village {attackerGarrison.VillageId} not found.");

            var camp = await _marchRepository.GetByIdAsync(march.TargetId, cancellationToken);
            var campGarrison = camp is null
                ? null
                : await _garrisonRepository.GetByIdAsync(camp.GarrisonId, cancellationToken);
            var campHome = campGarrison is null
                ? null
                : await _villageRepository.GetByIdAsync(campGarrison.VillageId, cancellationToken);

            // Табір могли відкликати, розбити чи забрати телепортом, поки марш ішов;
            // нападник — опинитись під щитом новачка. Бою немає — розворот
            if (camp is null || camp.State != MarchState.Camping || campHome is null
                || _status.IsShielded(attackerVillage))
            {
                _logistics.TurnMarchBack(march, attackerArmy, utcNow);
                return;
            }

            var defence = DefenceStacks.FromArmy(camp.GetUnits());

            var campHero = camp.HeroId is Guid campHeroId
                ? await _heroRepository.GetByIdAsync(campHeroId, cancellationToken)
                : null;

            var defenceBuffs = new DefenceBuffs(_heroModifiers.For(campHero), new Dictionary<Guid, StackBuff>());

            var attackerHero = march.HeroId is Guid heroId
                ? await _heroRepository.GetByIdAsync(heroId, cancellationToken)
                : null;

            var attackerBonus = await _effectResolver.GetMultiplierAsync(
                    attackerVillage.PlayerId, EffectTarget.Attack, utcNow, cancellationToken)
                * await _territoryBonus.AttackMultiplierAsync(attackerVillage, utcNow, cancellationToken);

            // Стін у полі немає (§2.5): лише територія клану — за клітинкою табору
            var defenderBonus = await _territoryBonus.DefenceMultiplierAtAsync(
                campHome.PlayerId, camp.TargetX, camp.TargetY, utcNow, cancellationToken);

            var seed = _random.Next(int.MaxValue);

            var outcome = _resolver.Resolve(attackerArmy, defence, terrain, seed,
                attackerBonus, defenderBonus, _logistics.CalculateWoundedCapacity(attackerVillage, attackerGarrison),
                _heroModifiers.For(attackerHero), defenceBuffs);

            var result = outcome.Battle;

            march.ApplyLosses(result.AttackerLosses, utcNow);
            attackerGarrison.AdmitWounded(outcome.AttackerCasualties.Wounded, utcNow);

            // Власник один — утрати по стеках складаються назад у тип+рівень
            var campLost = _lossAllocator.Allocate(defence, result.DefenderLosses, defenceBuffs)
                .Where(loss => loss.Lost > 0)
                .GroupBy(loss => new UnitStackKey(loss.UnitType, loss.Level))
                .ToDictionary(group => group.Key, group => group.Sum(loss => loss.Lost));

            await _aftermath.RecordAttackerAsync(march, attackerVillage, attackerGarrison,
                campHome.Name, _status.MainBuildingLevel(campHome), attackerArmy, outcome, terrain, seed, utcNow,
                cancellationToken);

            await _aftermath.RecordCampDefenceAsync(march, camp, campHome, attackerVillage, campLost, result,
                terrain, seed, utcNow, cancellationToken);

            if (result.AttackerWon)
                await RoutAsync(camp, campHome, utcNow, cancellationToken);

            _logistics.TurnMarchBack(march, march.GetUnits(), utcNow);

            _logger.LogInformation("Camp {CampId} of {Owner} attacked by {Attacker}: attacker {Outcome}",
                camp.Id, campHome.PlayerId, attackerVillage.PlayerId, result.AttackerWon ? "won" : "lost");
        }

        /// <summary>
        /// Програний табір знімається: уцілілі й герой відступають додому маршем.
        /// Не лишилось нікого — похід просто завершується.
        /// </summary>
        private async Task RoutAsync(March camp, Village campHome, DateTime utcNow, CancellationToken cancellationToken)
        {
            if (camp.GetUnits().Count == 0 && camp.HeroId is null)
            {
                camp.BreakCamp(campHome.X, campHome.Y, TimeSpan.Zero, utcNow);
                camp.Complete(utcNow);
                return;
            }

            await _campHomecoming.SendHomeAsync(camp, campHome, utcNow, cancellationToken);
        }
    }
}
