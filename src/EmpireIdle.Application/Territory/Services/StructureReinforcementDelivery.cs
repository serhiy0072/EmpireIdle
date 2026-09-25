using EmpireIdle.Application.Interfaces;
using EmpireIdle.Application.Marches.Services;
using EmpireIdle.Domain.Combat;
using EmpireIdle.Domain.Entities;
using EmpireIdle.Domain.Services;
using Microsoft.Extensions.Logging;

namespace EmpireIdle.Application.Territory.Services
{
    /// <summary>
    /// Прибуття маршу учасника до споруди свого клану (GDD §7.2): марш прискорює
    /// будівництво пропорційно своїй силі й лишається гарнізоном. Що не влізло —
    /// повертається додому, будівництво прискорює й повний гарнізон.
    ///
    /// Як і доставка в село, не кидає винятків: це прогін сканера, і невідповідність
    /// (споруду зруйновано, гравець вийшов із клану) — розворот, а не збій.
    /// </summary>
    public sealed class StructureReinforcementDelivery
    {
        private readonly IGarrisonRepository _garrisonRepository;
        private readonly IVillageRepository _villageRepository;
        private readonly IHeroRepository _heroRepository;
        private readonly IClanRepository _clanRepository;
        private readonly IClanStructureRepository _structureRepository;
        private readonly CombatCalculator _combat;
        private readonly HeroCombatModifiers _heroModifiers;
        private readonly TerrainGenerator _terrain;
        private readonly MarchLogistics _logistics;
        private readonly ClanTerritoryRules _rules;
        private readonly ILogger<StructureReinforcementDelivery> _logger;

        public StructureReinforcementDelivery(
            IGarrisonRepository garrisonRepository,
            IVillageRepository villageRepository,
            IHeroRepository heroRepository,
            IClanRepository clanRepository,
            IClanStructureRepository structureRepository,
            CombatCalculator combat,
            HeroCombatModifiers heroModifiers,
            TerrainGenerator terrain,
            MarchLogistics logistics,
            ClanTerritoryRules rules,
            ILogger<StructureReinforcementDelivery> logger)
        {
            _garrisonRepository = garrisonRepository;
            _villageRepository = villageRepository;
            _heroRepository = heroRepository;
            _clanRepository = clanRepository;
            _structureRepository = structureRepository;
            _combat = combat;
            _heroModifiers = heroModifiers;
            _terrain = terrain;
            _logistics = logistics;
            _rules = rules;
            _logger = logger;
        }

        public async Task DeliverAsync(March march, DateTime utcNow, CancellationToken cancellationToken)
        {
            var units = march.GetUnits();

            var ownerGarrison = await _garrisonRepository.GetByIdAsync(march.GarrisonId, cancellationToken)
                ?? throw new InvalidOperationException($"Garrison {march.GarrisonId} not found for march {march.Id}.");

            var ownerVillage = await _villageRepository.GetByIdAsync(ownerGarrison.VillageId, cancellationToken)
                ?? throw new InvalidOperationException($"Village {ownerGarrison.VillageId} not found for garrison {ownerGarrison.Id}.");

            var structure = await _structureRepository.GetByIdAsync(march.TargetId, cancellationToken);
            var structureGarrison = structure is null
                ? null
                : await _garrisonRepository.GetByIdAsync(structure.GarrisonId, cancellationToken);

            var clanId = await _clanRepository.GetClanIdByMemberAsync(ownerVillage.PlayerId, cancellationToken);

            if (structure is null || structureGarrison is null || clanId != structure.ClanId)
            {
                _logistics.TurnMarchBack(march, units, utcNow);
                return;
            }

            var hero = march.HeroId is Guid heroId
                ? await _heroRepository.GetByIdAsync(heroId, cancellationToken)
                : null;

            // Сила маршу, а не кількість маршів: один сильний прискорює більше за кілька слабких
            var power = _combat.CalculatePower(units, _terrain.GetTerrainType(march.ServerId, structure.X, structure.Y),
                isAttacker: true, _heroModifiers.For(hero));

            var accelerated = structure.Accelerate(_rules.BuildShareFor(power), _rules.MaxBuildShare, utcNow);

            var free = Math.Max(0, _rules.GarrisonCapacity - structureGarrison.ReinforcementCount);
            var (accepted, rejected) = ReinforcementSplit.Take(units, free);

            if (accepted.Count > 0)
                structureGarrison.AddReinforcements(ownerVillage.PlayerId, ownerGarrison.Id, accepted,
                    _rules.GarrisonCapacity, utcNow);

            // Герой лишається завжди: слота гарнізону він не займає, а бонус тримає над своїм стеком
            if (hero is not null)
            {
                var leader = await _heroRepository.GetLeaderAsync(structureGarrison.Id, hero.PlayerId, cancellationToken);

                hero.Arrive(structureGarrison.Id, leaderSlotFree: leader is null, utcNow);
            }

            if (rejected.Count > 0)
            {
                march.ApplyLosses(accepted, utcNow);
                march.LeaveHeroBehind(utcNow);

                _logistics.TurnMarchBack(march, rejected, utcNow);
            }
            else
            {
                march.Delivered(utcNow);
            }

            _logger.LogInformation(
                "March {MarchId} reached structure {StructureId}: build cut by {Share:P1}, {Accepted} units garrisoned, {Rejected} sent back",
                march.Id, structure.Id, accelerated, accepted.Values.Sum(), rejected.Values.Sum());
        }
    }
}
