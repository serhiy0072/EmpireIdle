using EmpireIdle.Application.Interfaces;
using EmpireIdle.Domain.Entities;
using EmpireIdle.Domain.Enums;
using EmpireIdle.Domain.Exceptions;
using EmpireIdle.Domain.Services;
using System.Net.NetworkInformation;

namespace EmpireIdle.Application.Marches.Services
{
    /// <summary>
    /// Ціль походу: де вона, як зветься і чим боронитиметься.
    /// </summary>
    /// <param name="Village">Село, якщо ціль — гравець. Для монстра null.</param>
    /// <param name="DefenderArmy">
    /// Склад оборони сумарно. Для села це гарнізон **разом із підкріпленнями**:
    /// саме так рахує бій, і прев'ю мусить бачити те саме.
    /// </param>
    public record MarchTarget(
        int X,
        int Y,
        string Name,
        int Level,
        Village? Village,
        Dictionary<string, int> DefenderArmy,
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
        private readonly MonsterArmyBuilder _armyBuilder;
        private readonly GameCatalog _catalog;
        private readonly VillageStatus _status;

        public MarchTargetResolver(
            IMonsterRepository monsterRepository,
            IVillageRepository villageRepository,
            IGarrisonRepository garrisonRepository,
            MonsterArmyBuilder armyBuilder,
            GameCatalog catalog,
            VillageStatus status)
        {
            _monsterRepository = monsterRepository;
            _villageRepository = villageRepository;
            _garrisonRepository = garrisonRepository;
            _armyBuilder = armyBuilder;
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
            CancellationToken cancellationToken)
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
                        _armyBuilder.BuildArmy(monster.Type, monster.Level),
                        // Монстр стін не має
                        DefenceMultiplier: 1.0);

                case MarchTargetType.Village:
                    var village = await _villageRepository.GetByIdAsync(targetId, cancellationToken)
                        ?? throw new EntityNotFoundException("Village", targetId);

                    if (village.ServerId != origin.ServerId)
                        throw new EntityNotFoundException("Village", targetId);

                    var garrison = await _garrisonRepository.GetByVillageIdAsync(village.Id, cancellationToken);

                    // Підкріплення входять в оборону — і в бою, і тут
                    var army = garrison is null
                        ? []
                        : garrison.GetDefence()
                            .GroupBy(s => s.UnitType)
                            .ToDictionary(g => g.Key, g => g.Sum(s => s.Count));

                    return new MarchTarget(
                        village.X, village.Y,
                        village.Name,
                        _status.MainBuildingLevel(village),
                        village,
                        army,
                        _status.DefenceMultiplier(village));

                default:
                    throw new RequirementNotMetException($"Unsupported target type '{targetType}'.");
            }
        }

        /// <summary>
        /// Щит новачка діє в обидва боки: гравець під ним не атакує,
        /// і його самого атакувати не можна. Монстрів це не стосується —
        /// PvE відкритий із першого рівня.
        /// </summary>
        public void EnsureAttackAllowed(Village origin, MarchTarget target)
        {
            if (target.Village is null)
                return;

            var shieldLevel = _catalog.Config.Combat.NewbieShieldTownHallLevel;

            if (_status.IsShielded(origin))
                throw new RequirementNotMetException(
                    $"Attacking other players is available from town hall level {shieldLevel}.");

            if (_status.IsShielded(target.Village))
                throw new RequirementNotMetException("This village is under a newbie shield.");
        }
    }
}
