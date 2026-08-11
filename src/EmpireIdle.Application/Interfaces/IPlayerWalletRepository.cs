using EmpireIdle.Domain.Entities;

namespace EmpireIdle.Application.Interfaces
{
    /// <summary>
    /// Репозиторій для роботи з PlayerWallet aggregate.
    /// </summary>
    public interface IPlayerWalletRepository : IRepository<PlayerWallet>
    {
        /// <summary>Знайти гаманець за ідентифікатором акаунта.</summary>
        Task<PlayerWallet?> GetByUserIdAsync(string userId, CancellationToken cancellationToken = default);
    }
}
