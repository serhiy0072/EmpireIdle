using EmpireIdle.Application.Interfaces;
using EmpireIdle.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace EmpireIdle.Infrastructure.Persistence.Repositories
{
    /// <summary>
    /// Реалізація репозиторію PlayerWallet через EF Core.
    ///
    /// Transactions навмисно НЕ підвантажуються: це append-only реєстр, що росте
    /// назавжди, а AddGems/SpendGems до нього лише дописують. EF вставить новий
    /// рядок і без завантаження існуючих. Для екрана історії потрібен окремий
    /// метод із пагінацією, а не Include тут.
    /// </summary>
    public class PlayerWalletRepository : IPlayerWalletRepository
    {
        private readonly AppDbContext _context;
        public PlayerWalletRepository(AppDbContext context)
        {
            _context = context;
        }

        /// <inheritdoc/>
        public async Task AddAsync(PlayerWallet entity, CancellationToken cancellationToken = default)
            => await _context.PlayerWallets.AddAsync(entity, cancellationToken);

        /// <inheritdoc/>
        public Task<PlayerWallet?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
            => _context.PlayerWallets.FirstOrDefaultAsync(pw => pw.Id == id, cancellationToken);

        /// <inheritdoc/>
        public Task<PlayerWallet?> GetByUserIdAsync(string userId, CancellationToken cancellationToken = default)
            => _context.PlayerWallets.FirstOrDefaultAsync(pw => pw.UserId == userId, cancellationToken);
    }
}
