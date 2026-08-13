
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
        /// <summary>Почати явну транзакцію (для операцій, що охоплюють кілька агрегатів).</summary>
        Task BeginTransactionAsync(CancellationToken cancellationToken = default);

        /// <summary>Підтвердити транзакцію.</summary>
        Task CommitTransactionAsync(CancellationToken cancellationToken = default);

        /// <summary>Відкотити транзакцію.</summary>
        Task RollbackTransactionAsync(CancellationToken cancellationToken = default);
    }
}
