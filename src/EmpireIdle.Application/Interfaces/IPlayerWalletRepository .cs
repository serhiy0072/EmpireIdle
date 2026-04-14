using EmpireIdle.Domain.Entities;
using System.Threading;
using System.Threading.Tasks;

namespace EmpireIdle.Application.Interfaces
{
    /// <summary>
    /// Репозиторій для роботи з PlayerWallet aggregate.
    /// </summary>
    public interface IPlayerWalletRepository : IRepository<PlayerWallet>
    {
        /// <summary>Знайти гаманець за ідентифікатором гравця.</summary>
        Task<PlayerWallet?> GetByPlayerIdAsync(Guid playerId, CancellationToken cancellationToken = default)
    }
}
