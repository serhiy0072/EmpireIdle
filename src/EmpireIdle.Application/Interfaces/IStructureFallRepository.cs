using EmpireIdle.Domain.Entities;

namespace EmpireIdle.Application.Interfaces
{
    /// <summary>Історія зруйнованих кланових споруд (GDD §7.2).</summary>
    public interface IStructureFallRepository
    {
        Task AddAsync(StructureFall fall, CancellationToken cancellationToken = default);

        Task<StructureFall?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
    }
}
