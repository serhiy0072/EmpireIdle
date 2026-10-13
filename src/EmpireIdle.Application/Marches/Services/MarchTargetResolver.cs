using EmpireIdle.Application.Common.Services;
using EmpireIdle.Application.Interfaces;
using EmpireIdle.Application.Territory.Services;
using EmpireIdle.Domain.Combat;
using EmpireIdle.Domain.Entities;
using EmpireIdle.Domain.Enums;
using EmpireIdle.Domain.Exceptions;
using EmpireIdle.Domain.Services;
using EmpireIdle.Domain.ValueObjects;

namespace EmpireIdle.Application.Marches.Services
{
    /// <param name="Village">Село, якщо ціль — гравець. Для монстра null.</param>
    /// <param name="Defence">
    /// Склад оборони по стеках. Для села це гарнізон **разом із підкріпленнями**:
    /// саме так рахує бій, і прев'ю мусить бачити те саме.
    /// </param>
    /// <param name="DefenceBuffs">
    /// Пасивки лідерів, що стоять у цій обороні. Для монстра порожні.
    /// </param>
    /// <param name="Structure">Кланова споруда, якщо ціль — вона. Name тоді — тег клану.</param>
    /// <param name="Camp">Марш-табір, якщо ціль — він (§2.5). Name тоді — село власника.</param>
    /// <param name="CampHome">Село власника табору: хто він, куди відступає армія й куди йдуть поранені.</param>
    public record MarchTarget(
        int X,
        int Y,
        string Name,
        int Level,
        Village? Village,
        IReadOnlyList<DefenceStack> Defence,
        DefenceBuffs DefenceBuffs,
        double DefenceMultiplier,
        ClanStructure? Structure = null,
        March? Camp = null,
        Village? CampHome = null);

    /// <summary>
    /// Знаходить ціль походу й описує її однаково для відправлення,
    /// прев'ю та бою.
    ///
    /// Живе окремо, бо розходження між прев'ю й боєм — не теоретичне:
    /// прев'ю рахувало захисника без кланових підкріплень, не бачило
    /// щита новачка й не звіряло світ. Гравець приймав рішення за
    /// числами, яких у бою не було.
    /// </summary>
    public sealed class MarchTargetResolver
    {
        private readonly IMonsterRepository _monsterRepository;
        private readonly IVillageRepository _villageRepository;
        private readonly IGarrisonRepository _garrisonRepository;
        private readonly IHeroRepository _heroRepository;
        private readonly MonsterArmyBuilder _armyBuilder;
        private readonly HeroCombatModifiers _heroModifiers;
        private readonly GameCatalog _catalog;
        private readonly VillageStatus _status;
        private readonly IClanStructureRepository _structureRepository;
        private readonly IClanRepository _clanRepository;
        private readonly ClanTerritoryRules _territory;
        private readonly TerritoryBonus _territoryBonus;
        private readonly IMarchRepository _marchRepository;
        private readonly HostilityRules _hostility;
        private readonly EffectResolver _effects;

        public MarchTargetResolver(
            IMonsterRepository monsterRepository,
            IVillageRepository villageRepository,
            IGarrisonRepository garrisonRepository,
            IHeroRepository heroRepository,
            MonsterArmyBuilder armyBuilder,
            HeroCombatModifiers heroModifiers,
            GameCatalog catalog,
            VillageStatus status,
            IClanStructureRepository structureRepository,
            IClanRepository clanRepository,
            ClanTerritoryRules territory,
            IMarchRepository marchRepository,
            EffectResolver effects)
        {
            _monsterRepository = monsterRepository;
            _villageRepository = villageRepository;
            _garrisonRepository = garrisonRepository;
            _heroRepository = heroRepository;
            _armyBuilder = armyBuilder;
            _heroModifiers = heroModifiers;
            _status = status;
            _catalog = catalog;
            _structureRepository = structureRepository;
            _clanRepository = clanRepository;
            _territory = territory;
            _marchRepository = marchRepository;
            _effects = effects;

            // Той самий розрахунок, що в бою: прев'ю не має розходитись із результатом
            _territoryBonus = new TerritoryBonus(clanRepository, structureRepository, territory);
            _hostility = new HostilityRules(clanRepository);
        }

        /// <summary>Множник атаки кланової території для маршу з цього села — для прев'ю.</summary>
        public Task<double> AttackMultiplierAsync(Village origin, DateTime utcNow, CancellationToken cancellationToken)
            => _territoryBonus.AttackMultiplierAsync(origin, utcNow, cancellationToken);

        /// <summary>
        /// Резолвить ціль і звіряє світ.
        ///
        /// TargetId приходить від клієнта, а query-фільтр захищає лише читання
        /// в межах поточного світу — тож без явної звірки ціль з чужого світу
        /// виглядала б валідною.
        /// </summary>
        public async Task<MarchTarget> ResolveAsync(MarchTargetType targetType, Guid targetId, Village origin,
            DateTime utcNow, CancellationToken cancellationToken)
        {
            switch (targetType)
            {
                case MarchTargetType.Monster:
                    var monster = await _monsterRepository.GetByIdAsync(targetId, cancellationToken)
                        ?? throw new EntityNotFoundException("Monster", targetId);

                    if (monster.ServerId != origin.ServerId)
                        throw new EntityNotFoundException("Monster", targetId);

                    return new MarchTarget(
                        monster.X, monster.Y,
                        _catalog.MonsterName(monster.Type),
                        monster.Level,
                        Village: null,
                        // Монстри — не гравці, свого рівня юнітів не мають: завжди 1
                        DefenceStacks.FromArmy(_armyBuilder.BuildArmy(monster.Type, monster.Level)
                            .ToDictionary(kv => new UnitStackKey(kv.Key, 1), kv => kv.Value)),
                        // Монстр ні стін, ні героїв не має
                        DefenceBuffs.None,
                        DefenceMultiplier: 1.0);

                case MarchTargetType.Village:
                    var village = await _villageRepository.GetByIdAsync(targetId, cancellationToken)
                        ?? throw new EntityNotFoundException("Village", targetId);

                    if (village.ServerId != origin.ServerId)
                        throw new EntityNotFoundException("Village", targetId);

                    var garrison = await _garrisonRepository.GetByVillageIdAsync(village.Id, cancellationToken);

                    // Підкріплення входять в оборону — і в бою, і тут
                    var defence = garrison is null
                        ? []
                        : garrison.GetDefence();

                    var buffs = garrison is null
                        ? DefenceBuffs.None
                        : await BuildDefenceBuffsAsync(garrison.Id, village.PlayerId, cancellationToken);

                    return new MarchTarget(
                        village.X, village.Y,
                        village.Name,
                        _status.MainBuildingLevel(village),
                        village,
                        defence,
                        buffs,
                        _status.DefenceMultiplier(village, utcNow)
                        * await _territoryBonus.DefenceMultiplierAsync(village, utcNow, cancellationToken)
                        * await _effects.GetMultiplierAsync(village.PlayerId, EffectTarget.Defense, utcNow, cancellationToken));

                case MarchTargetType.ClanStructure:
                    var structure = await _structureRepository.GetByIdAsync(targetId, cancellationToken)
                        ?? throw new EntityNotFoundException("Clan structure", targetId);

                    if (structure.ServerId != origin.ServerId)
                        throw new EntityNotFoundException("Clan structure", targetId);

                    var structureGarrison = await _garrisonRepository.GetByIdAsync(structure.GarrisonId, cancellationToken);

                    // Своїх юнітів у споруди немає: уся оборона — підкріплення учасників,
                    // і лідер кожного діє лише на свій стек
                    var structureDefence = structureGarrison is null ? [] : structureGarrison.GetDefence();

                    var structureBuffs = structureGarrison is null
                        ? DefenceBuffs.None
                        : await BuildDefenceBuffsAsync(structureGarrison.Id, Guid.Empty, cancellationToken);

                    var clan = await _clanRepository.GetCardAsync(structure.ClanId, cancellationToken);

                    return new MarchTarget(
                        structure.X, structure.Y,
                        clan?.Tag ?? string.Empty,
                        Level: 0,
                        Village: null,
                        structureDefence,
                        structureBuffs,
                        // Споруда завжди в межах власного радіуса: добудована — під своїм бонусом
                        _territory.DefenceMultiplier(_territory.Enabled && structure.IsActiveAt(utcNow)),
                        structure);

                case MarchTargetType.Camp:
                    // Табором вважається лише марш, що стоїть; відкликаний чи розбитий — уже не ціль
                    var camp = await _marchRepository.GetByIdAsync(targetId, cancellationToken);

                    if (camp is null || camp.State != MarchState.Camping || camp.ServerId != origin.ServerId)
                        throw new EntityNotFoundException("Camp", targetId);

                    var campGarrison = await _garrisonRepository.GetByIdAsync(camp.GarrisonId, cancellationToken);
                    var campHome = campGarrison is null
                        ? null
                        : await _villageRepository.GetByIdAsync(campGarrison.VillageId, cancellationToken);

                    if (campHome is null)
                        throw new EntityNotFoundException("Camp", targetId);

                    var campHero = camp.HeroId is Guid campHeroId
                        ? await _heroRepository.GetByIdAsync(campHeroId, cancellationToken)
                        : null;

                    return new MarchTarget(
                        camp.TargetX, camp.TargetY,
                        campHome.Name,
                        _status.MainBuildingLevel(campHome),
                        Village: null,
                        // Армія табору — одного власника; його герой веде її й у обороні
                        DefenceStacks.FromArmy(camp.GetUnits()),
                        new DefenceBuffs(_heroModifiers.For(campHero), new Dictionary<Guid, StackBuff>()),
                        // Стін у полі немає (§2.5); територія — за клітинкою табору
                        await _territoryBonus.DefenceMultiplierAtAsync(
                            campHome.PlayerId, camp.TargetX, camp.TargetY, utcNow, cancellationToken),
                        Camp: camp,
                        CampHome: campHome);

                default:
                    throw new RequirementNotMetException($"Unsupported target type '{targetType}'.");
            }
        }

        /// <summary>
        /// Пасивки лідерів гарнізону. Лідер господаря діє на його юнітів,
        /// лідер кожного союзника — лише на власний стек.
        /// </summary>
        private async Task<DefenceBuffs> BuildDefenceBuffsAsync(Guid garrisonId, Guid hostPlayerId,
            CancellationToken cancellationToken)
        {
            var stationed = await _heroRepository.GetByGarrisonAsync(garrisonId, cancellationToken);

            return new DefenceBuffs(
                _heroModifiers.For(stationed.FirstOrDefault(h => h.IsLeader && h.PlayerId == hostPlayerId)),
                stationed
                    .Where(h => h.IsLeader && h.PlayerId != hostPlayerId)
                    .ToDictionary(h => h.PlayerId, h => _heroModifiers.For(h)));
        }

        /// <summary>
        /// Щит новачка діє в обидва боки: гравець під ним не атакує,
        /// і його самого атакувати не можна. Монстрів це не стосується —
        /// PvE відкритий із першого рівня. Щит після падіння захищає лише
        /// ціль: власний напад його знімає, а не забороняється ним.
        /// </summary>
        /// <summary>
        /// Усі правила нападу на гравця: спершу хто кому ворог (своє й соклановців не
        /// атакують), потім щити. Те саме бачать і відправка, і прев'ю.
        /// </summary>
        public async Task EnsureAttackAllowedAsync(Village origin, MarchTarget target, DateTime utcNow,
            CancellationToken cancellationToken)
        {
            var defender = target.Village?.PlayerId ?? target.CampHome?.PlayerId;

            if (defender is Guid defenderId
                && await _hostility.RefusalAsync(origin.PlayerId, defenderId, target.Camp is not null, cancellationToken)
                    is { } refusal)
                throw new RequirementNotMetException(refusal, $"Player {origin.PlayerId} cannot attack {defenderId}.");

            EnsureAttackAllowed(origin, target, utcNow);
        }

        public void EnsureAttackAllowed(Village origin, MarchTarget target, DateTime utcNow)
        {
            // Споруда клану й табір — теж PvP: новачок під щитом їх не атакує, але щита в них самих немає
            if (target.Village is null && target.Structure is null && target.Camp is null)
                return;


            var shieldLevel = _catalog.Config.Combat.NewbieShieldTownHallLevel;

            if (_status.IsShielded(origin))
                throw new RequirementNotMetException(RefusalReasons.MarchOwnShield,
                    $"Attacking other players is available from town hall level {shieldLevel}.", shieldLevel);

            if (target.Village is null)
                return;

            if (_status.IsShielded(target.Village))
                throw new RequirementNotMetException(RefusalReasons.MarchTargetShielded, "This village is under a newbie shield.");

            if (target.Village.IsShieldedAt(utcNow))
                throw new RequirementNotMetException(RefusalReasons.MarchTargetFallShield, "This village has recently fallen and is shielded.");
        }
    }
}
