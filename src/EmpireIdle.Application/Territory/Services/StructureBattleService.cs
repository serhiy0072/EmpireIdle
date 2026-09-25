using EmpireIdle.Application.Common.Services;
using EmpireIdle.Application.Interfaces;
using EmpireIdle.Application.Marches.Services;
using EmpireIdle.Domain.Combat;
using EmpireIdle.Domain.Entities;
using EmpireIdle.Domain.Enums;
using EmpireIdle.Domain.Services;
using EmpireIdle.Domain.ValueObjects;
using Microsoft.Extensions.Logging;

namespace EmpireIdle.Application.Territory.Services
{
    /// <summary>
    /// Бій за кланову споруду (GDD §7.2). Той самий бій, що за село, але без
    /// господаря й без грабунку: оборона — гарнізон учасників, поранені йдуть
    /// у госпіталі власників, а перемога нападника руйнує споруду й знімає бонус одразу.
    /// </summary>
    public sealed class StructureBattleService
    {
        private readonly IGarrisonRepository _garrisonRepository;
        private readonly IVillageRepository _villageRepository;
        private readonly IHeroRepository _heroRepository;
        private readonly IClanRepository _clanRepository;
        private readonly IClanStructureRepository _structureRepository;
        private readonly IRandomSource _random;
        private readonly BattleResolver _resolver;
        private readonly DefenceLossAllocator _lossAllocator;
        private readonly EffectResolver _effectResolver;
        private readonly MarchLogistics _logistics;
        private readonly BattleAftermath _aftermath;
        private readonly VillageStatus _status;
        private readonly HeroCombatModifiers _heroModifiers;
        private readonly ClanTerritoryRules _rules;
        private readonly TerritoryBonus _territoryBonus;
        private readonly ClanStructureRemover _remover;
        private readonly IStructureFallRepository _falls;
        private readonly ILogger<StructureBattleService> _logger;

        public StructureBattleService(
            IGarrisonRepository garrisonRepository,
            IVillageRepository villageRepository,
            IHeroRepository heroRepository,
            IClanRepository clanRepository,
            IClanStructureRepository structureRepository,
            IRandomSource random,
            BattleResolver resolver,
            DefenceLossAllocator lossAllocator,
            EffectResolver effectResolver,
            MarchLogistics logistics,
            BattleAftermath aftermath,
            VillageStatus status,
            HeroCombatModifiers heroModifiers,
            ClanTerritoryRules rules,
            TerritoryBonus territoryBonus,
            ClanStructureRemover remover,
            IStructureFallRepository falls,
            ILogger<StructureBattleService> logger)
        {
            _garrisonRepository = garrisonRepository;
            _villageRepository = villageRepository;
            _heroRepository = heroRepository;
            _clanRepository = clanRepository;
            _structureRepository = structureRepository;
            _random = random;
            _resolver = resolver;
            _lossAllocator = lossAllocator;
            _effectResolver = effectResolver;
            _logistics = logistics;
            _aftermath = aftermath;
            _status = status;
            _heroModifiers = heroModifiers;
            _rules = rules;
            _territoryBonus = territoryBonus;
            _remover = remover;
            _falls = falls;
            _logger = logger;
        }

        public async Task ResolveAsync(March march, Dictionary<UnitStackKey, int> attackerArmy,
            string terrain, DateTime utcNow, CancellationToken cancellationToken)
        {
            var attackerGarrison = await _garrisonRepository.GetByIdAsync(march.GarrisonId, cancellationToken)
                ?? throw new InvalidOperationException($"Garrison {march.GarrisonId} not found for march {march.Id}.");

            var attackerVillage = await _villageRepository.GetByIdAsync(attackerGarrison.VillageId, cancellationToken)
                ?? throw new InvalidOperationException($"Village {attackerGarrison.VillageId} not found.");

            var structure = await _structureRepository.GetByIdAsync(march.TargetId, cancellationToken);
            var defenderGarrison = structure is null
                ? null
                : await _garrisonRepository.GetByIdAsync(structure.GarrisonId, cancellationToken);

            var attackerClanId = await _clanRepository.GetClanIdByMemberAsync(attackerVillage.PlayerId, cancellationToken);

            // Споруду могли знести, нападник — вступити в її клан або опинитись під щитом новачка
            if (structure is null || defenderGarrison is null
                || attackerClanId == structure.ClanId
                || _status.IsShielded(attackerVillage))
            {
                _logistics.TurnMarchBack(march, attackerArmy, utcNow);
                return;
            }

            var defence = defenderGarrison.GetDefence();

            // Господаря немає: лідер кожного учасника діє лише на свій стек
            var stationed = await _heroRepository.GetByGarrisonAsync(defenderGarrison.Id, cancellationToken);

            var defenceBuffs = new DefenceBuffs(
                StackBuff.None,
                stationed.Where(h => h.IsLeader).ToDictionary(h => h.PlayerId, h => _heroModifiers.For(h)));

            var attackerHero = march.HeroId is Guid heroId
                ? await _heroRepository.GetByIdAsync(heroId, cancellationToken)
                : null;

            var attackerBonus = await _effectResolver.GetMultiplierAsync(
                    attackerVillage.PlayerId, EffectTarget.Attack, utcNow, cancellationToken)
                * await _territoryBonus.AttackMultiplierAsync(attackerVillage, utcNow, cancellationToken);

            // Добудована споруда стоїть у власному радіусі — під своїм бонусом
            var defenderBonus = _rules.DefenceMultiplier(_rules.Enabled && structure.IsActiveAt(utcNow));

            var seed = _random.Next(int.MaxValue);

            var outcome = _resolver.Resolve(attackerArmy, defence, terrain, seed,
                attackerBonus, defenderBonus, _logistics.CalculateWoundedCapacity(attackerVillage, attackerGarrison),
                _heroModifiers.For(attackerHero), defenceBuffs);

            var result = outcome.Battle;

            march.ApplyLosses(result.AttackerLosses, utcNow);
            attackerGarrison.AdmitWounded(outcome.AttackerCasualties.Wounded, utcNow);

            var defenderLosses = _lossAllocator.Allocate(defence, result.DefenderLosses, defenceBuffs);
            defenderGarrison.ApplyDefenceLosses(defenderLosses, utcNow);

            var clan = await _clanRepository.GetCardAsync(structure.ClanId, cancellationToken);

            await _aftermath.RecordAttackerAsync(march, attackerVillage, attackerGarrison,
                clan?.Tag ?? string.Empty, 0, attackerArmy, outcome, terrain, seed, utcNow, cancellationToken);

            // Власника-господаря немає: кожен, хто стояв у гарнізоні, отримує свій звіт,
            // а після поразки всі йдуть додому
            await _aftermath.RecordStructureDefenceAsync(march, structure, defenderGarrison, attackerVillage,
                defence, defenderLosses, result, terrain, seed, utcNow, cancellationToken);

            if (result.AttackerWon)
            {
                structure.MarkDestroyed(utcNow);

                // Запис у історію — на нього посилатимуться листи учасникам клану
                await _falls.AddAsync(new StructureFall(Guid.NewGuid(), structure.ServerId, structure,
                    attackerVillage.PlayerId, attackerVillage.Name, utcNow), cancellationToken);
                await _remover.RemoveAsync(structure, utcNow, cancellationToken);
            }

            _logistics.TurnMarchBack(march, march.GetUnits(), utcNow);

            _logger.LogInformation("Structure {StructureId} of clan {ClanId} attacked by {Attacker}: attacker {Outcome}",
                structure.Id, structure.ClanId, attackerVillage.PlayerId, result.AttackerWon ? "won" : "lost");
        }
    }
}
