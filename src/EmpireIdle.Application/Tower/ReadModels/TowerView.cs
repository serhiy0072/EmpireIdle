using EmpireIdle.Domain.Services.Config;

namespace EmpireIdle.Application.Tower.ReadModels
{
    /// <summary>Вежа очима гравця: прогрес, наступний поверх із ворогом і нагорода найближчих поверхів.</summary>
    /// <param name="Unlocked">Чи відкрила ратуша вежу.</param>
    /// <param name="Rank">Місце в рейтингу сервера; null — ще жодного поверху.</param>
    /// <param name="Next">Поверх, що чекає штурму; null — вежу підкорено.</param>
    /// <param name="Upcoming">Наступні поверхи з нагородами — стрічка вежі на екрані.</param>
    /// <param name="AttackBuff">Множник атаки від бустів і звірів, що діятиме в бою просто зараз.</param>
    /// <param name="DefenseBuff">Множник захисту від бустів і звірів.</param>
    public record TowerView(
        bool Unlocked,
        int RequiresMainBuildingLevel,
        int MaxFloor,
        int HighestFloor,
        int? Rank,
        TowerFloorView? Next,
        IReadOnlyList<TowerUpcomingFloor> Upcoming,
        double AttackBuff,
        double DefenseBuff);

    /// <summary>Ворог поверху — склад видно до бою, без цього контргри не буде.</summary>
    /// <param name="SetName">Що носять тіні: «4 рідкісні».</param>
    public record TowerFloorView(
        int Number,
        bool IsBoss,
        string ArchetypeKey,
        string ArchetypeName,
        string SetName,
        int HeroLevel,
        int UnitLevel,
        IReadOnlyList<TowerEnemyView> Enemies,
        IReadOnlyList<RewardConfig> Rewards);

    public record TowerEnemyView(int Lane, string HeroKey, string HeroClass, bool Boss);

    public record TowerUpcomingFloor(int Number, bool IsBoss, IReadOnlyList<RewardConfig> Rewards);

    /// <summary>Рядок рейтингу вежі.</summary>
    public record TowerLeaderboardEntry(int Rank, Guid PlayerId, string PlayerName, int HighestFloor, DateTime? ReachedAt);

    /// <param name="Mine">Власний рядок, навіть якщо гравець поза топом; null — ще жодного поверху.</param>
    public record TowerLeaderboard(IReadOnlyList<TowerLeaderboardEntry> Entries, TowerLeaderboardEntry? Mine);
}
