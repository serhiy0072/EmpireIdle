
namespace EmpireIdle.Application.Interfaces
{
    /// <summary>
    /// Unit of Work — зберігає всі зміни в базі даних за одну транзакцію.
    /// Викликається після всіх операцій над репозиторіями.
    /// </summary>
    public interface IUnitOfWork 
    {
        /// <summary>Зберегти всі зміни в базі даних.</summary>
        Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);

        /// <summary>
        /// Зберігає зміни, а на конфлікті оптимістичного блокування (xmin) повертає false
        /// й скидає все відстежене: пачка фонового джоба, що зіткнулась із гравцем, не має
        /// відкотити решту світу. Після false сутності з цього контексту — відірвані.
        /// </summary>
        Task<bool> TrySaveChangesAsync(CancellationToken cancellationToken = default);
        /// <summary>Почати явну транзакцію (для операцій, що охоплюють кілька агрегатів).</summary>
        Task BeginTransactionAsync(CancellationToken cancellationToken = default);

        /// <summary>Підтвердити транзакцію.</summary>
        Task CommitTransactionAsync(CancellationToken cancellationToken = default);

        /// <summary>Відкотити транзакцію.</summary>
        Task RollbackTransactionAsync(CancellationToken cancellationToken = default);
    }
}
