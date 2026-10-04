using EmpireIdle.Domain.Entities;

namespace EmpireIdle.Application.Interfaces
{
    /// <summary>Звіринець гравця: один на гравця, створюється з першим приручённям.</summary>
    public interface IBeastPenRepository
    {
        /// <summary>Звіринець разом зі звірами й лічильниками гарантії або null, якщо гравець ще нікого не приручав.</summary>
        Task<BeastPen?> GetByPlayerAsync(Guid playerId, CancellationToken cancellationToken = default);

        /// <summary>Те саме без трекінгу — для читання.</summary>
        Task<BeastPen?> GetByPlayerReadOnlyAsync(Guid playerId, CancellationToken cancellationToken = default);

        Task AddAsync(BeastPen pen, CancellationToken cancellationToken = default);
    }
}
