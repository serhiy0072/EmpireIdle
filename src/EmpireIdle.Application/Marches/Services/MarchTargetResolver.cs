using EmpireIdle.Application.Interfaces;
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
    public record MarchTarget(
        int X,
        int Y,
        string Name,
        int Level,
        Village? Village,
        IReadOnlyList<DefenceStack> Defence,
        DefenceBuffs DefenceBuffs,
        double DefenceMultiplier);

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

        public MarchTargetResolver(
            IMonsterRepository monsterRepository,
            IVillageRepository villageRepository,
            IGarrisonRepository garrisonRepository,
            IHeroRepository heroRepository,
            MonsterArmyBuilder armyBuilder,
            HeroCombatModifiers heroModifiers,
            GameCatalog catalog,
            VillageStatus status)
        {
            _monsterRepository = monsterRepository;
            _villageRepository = villageRepository;
            _garrisonRepository = garrisonRepository;
            _heroRepository = heroRepository;
            _armyBuilder = armyBuilder;
            _heroModifiers = heroModifiers;
            _status = status;
            _catalog = catalog;
        }

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
                        $"{monster.Type} (lvl {monster.Level})",
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
                        _status.DefenceMultiplier(village, utcNow));

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
        public void EnsureAttackAllowed(Village origin, MarchTarget target, DateTime utcNow)
        {
            if (target.Village is null)
                return;

            var shieldLevel = _catalog.Config.Combat.NewbieShieldTownHallLevel;

            if (_status.IsShielded(origin))
                throw new RequirementNotMetException(RefusalReasons.MarchOwnShield,
                    $"Attacking other players is available from town hall level {shieldLevel}.", shieldLevel);

            if (_status.IsShielded(target.Village))
                throw new RequirementNotMetException(RefusalReasons.MarchTargetShielded, "This village is under a newbie shield.");

            if (target.Village.IsShieldedAt(utcNow))
                throw new RequirementNotMetException(RefusalReasons.MarchTargetFallShield, "This village has recently fallen and is shielded.");
        }
    }
}
