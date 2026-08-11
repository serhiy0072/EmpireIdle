using EmpireIdle.Application.Interfaces;
using EmpireIdle.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace EmpireIdle.Infrastructure.Persistence.Repositories
{
    /// <summary>
    /// Реалізація репозиторію PlayerWallet через EF Core.
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
            => _context.PlayerWallets
            .Include(pw => pw.Transactions)
            .FirstOrDefaultAsync(pw =>  pw.Id == id, cancellationToken);

        /// <inheritdoc/>
        public Task<PlayerWallet?> GetByPlayerIdAsync(Guid playerId, CancellationToken cancellationToken = default)
            => _context.PlayerWallets
            .Include(pw => pw.Transactions)
            .FirstOrDefaultAsync(pw => pw.PlayerId == playerId, cancellationToken);
    }
}
