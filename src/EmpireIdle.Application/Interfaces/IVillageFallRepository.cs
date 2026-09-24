using EmpireIdle.Domain.Entities;

namespace EmpireIdle.Application.Interfaces
{
    /// <summary>Історія падінь міст (GDD §2.6).</summary>
    public interface IVillageFallRepository
    {
        Task AddAsync(VillageFall fall, CancellationToken cancellationToken = default);

        Task<VillageFall?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
    }
}
