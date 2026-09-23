using EmpireIdle.Domain.Dungeons;
using EmpireIdle.Domain.Enums;

namespace EmpireIdle.Application.Dungeons.Contracts
{
    /// <summary>Вітрина данжів: що доступно, за скільки й що з нього падає.</summary>
    public record DungeonsOverview(
        int Energy,
        int MaxEnergy,
        int EnergyPerRun,
        DateTime? EnergyFullAt,
        int TeamSize,
        Guid? ActiveRunId,
        IReadOnlyList<DungeonView> Dungeons);

    /// <param name="ClearedLevel">Найвищий зачищений рівень; 0 — данж ще не пройдено.</param>
    /// <param name="UnlockedLevel">Найвищий доступний рівень: пройдений плюс один.</param>
    public record DungeonView(
        string Key,
        string DisplayName,
        string Description,
        string ArtifactSetKey,
        int RequiresMainBuildingLevel,
        bool IsUnlocked,
        int ClearedLevel,
        int UnlockedLevel,
        IReadOnlyList<DungeonLevelView> Levels);

    /// <param name="Waves">Скільки боїв у забігу цього рівня, включно з босом.</param>
    public record DungeonLevelView(
        int Level,
        string ArtifactRarity,
        int Waves,
        double PowerMultiplier,
        IReadOnlyList<RewardLine> Reward);

    public record RewardLine(string Resource, int Amount);

    /// <summary>Поточний бій для клієнта: стан плюс журнал ходів, які ще не програні.</summary>
    public record DungeonRunView(
        Guid RunId,
        string DungeonKey,
        int Level,
        DungeonRunState State,
        int Wave,
        int WaveCount,
        int? ActorIndex,
        IReadOnlyList<CombatantView> Combatants,
        IReadOnlyList<TurnLog> Turns,
        DungeonRewardView? Reward);

    /// <param name="Abilities">Вміння героя; у ворогів порожньо.</param>
    public record CombatantView(
        int Index,
        string Side,
        string Line,
        string Key,
        string DisplayName,
        Guid? HeroId,
        double Attack,
        double Defense,
        double Health,
        double MaxHealth,
        double Shield,
        double Speed,
        int Energy,
        IReadOnlyList<StatusView> Statuses,
        IReadOnlyList<AbilityView> Abilities);

    public record StatusView(string Kind, double Magnitude, int TurnsLeft);

    /// <param name="Ready">Енергії вистачає просто зараз.</param>
    public record AbilityView(
        string Key,
        string DisplayName,
        string Description,
        int EnergyCost,
        string Target,
        bool Ready);

    /// <summary>Що видано за успішний забіг.</summary>
    public record DungeonRewardView(IReadOnlyList<RewardLine> Resources, IReadOnlyList<ArtifactDropView> Artifacts);

    public record ArtifactDropView(string ItemKey, string DisplayName, string Rarity, string SetKey);
}
