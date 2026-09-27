using EmpireIdle.Domain.Entities;

namespace EmpireIdle.Application.Interfaces
{
    public interface IServerRepository
    {
        Task<Server?> GetByIdAsync(int id, CancellationToken cancellationToken = default);

        /// <summary>
        /// Рівень світу без завантаження агрегату. Читається на кожен запит,
        /// що торкається виробітку чи геометрії — тому лише одне число.
        /// </summary>
        Task<int> GetLevelAsync(int id, CancellationToken cancellationToken = default);
    }
}
