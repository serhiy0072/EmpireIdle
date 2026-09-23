using EmpireIdle.Domain.Entities;

namespace EmpireIdle.Application.Interfaces
{
    /// <summary>Забіги, енергія й прогрес данжів одного гравця.</summary>
    public interface IDungeonRepository
    {
        /// <summary>Незавершений забіг гравця або null. Гравець веде лише один забіг за раз.</summary>
        Task<DungeonRun?> GetActiveRunAsync(Guid playerId, CancellationToken cancellationToken = default);

        Task<DungeonRun?> GetRunByIdAsync(Guid runId, CancellationToken cancellationToken = default);

        Task AddRunAsync(DungeonRun run, CancellationToken cancellationToken = default);

        /// <summary>Шкала енергії гравця або null, якщо він ще жодного разу не заходив.</summary>
        Task<DungeonEnergy?> GetEnergyAsync(Guid playerId, CancellationToken cancellationToken = default);

        Task AddEnergyAsync(DungeonEnergy energy, CancellationToken cancellationToken = default);

        /// <summary>Пройдені рівні: ключ данжу до найвищого зачищеного рівня.</summary>
        Task<Dictionary<string, int>> GetClearedLevelsAsync(Guid playerId, CancellationToken cancellationToken = default);

        Task AddClearAsync(DungeonClear clear, CancellationToken cancellationToken = default);
    }
}
