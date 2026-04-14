using System.Threading;
using System.Threading.Tasks;

namespace EmpireIdle.Application.Interfaces
{
    /// <summary>
    /// Unit of Work — зберігає всі зміни в базі даних за одну транзакцію.
    /// Викликається після всіх операцій над репозиторіями.
    /// </summary>
    public interface IUnitOfWork : IDisposable
    {
        /// <summary>Зберегти всі зміни в базі даних.</summary>
        Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
    }
}
