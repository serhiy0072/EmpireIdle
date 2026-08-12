using EmpireIdle.Domain.Entities;

namespace EmpireIdle.Application.Interfaces
{
    /// <summary>Репозиторій платежів за пакети gems.</summary>
    public interface IPaymentRepository : IRepository<Payment>
    {
        /// <summary>Знайти платіж за Id сесії Checkout — зв'язок із вебхуком.</summary>
        Task<Payment?> GetBySessionIdAsync(string sessionId, CancellationToken cancellationToken = default);
    }
}