using EmpireIdle.Domain.Entities;

namespace EmpireIdle.Application.Interfaces
{
    /// <summary>Репозиторій активних ефектів.</summary>
    public interface IActiveEffectRepository
    {
        /// <summary>Усі ефекти гравця (включно з простроченими).</summary>
        Task<List<ActiveEffect>> GetByPlayerAsync(Guid playerId, CancellationToken cancellationToken = default);

        /// <summary>Ефект на конкретну ціль; null — немає.</summary>
        Task<ActiveEffect?> GetAsync(Guid playerId, EffectTarget target, CancellationToken cancellationToken = default);

        /// <summary>Додати ефект.</summary>
        Task AddAsync(ActiveEffect effect, CancellationToken cancellationToken = default);

        /// <summary>Прибрати прострочені ефекти (фонове очищення).</summary>
        Task<int> RemoveExpiredAsync(DateTime utcNow, CancellationToken cancellationToken = default);
    }
}