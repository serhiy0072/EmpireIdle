using EmpireIdle.Application.Interfaces;
using EmpireIdle.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace EmpireIdle.Infrastructure.Persistence.Repositories
{
    /// <summary>
    /// Реалізація репозиторію Player через EF Core.
    /// </summary>
    public class PlayerRepository : IPlayerRepository
    {
        private readonly AppDbContext _context;
        public PlayerRepository(AppDbContext context)
        {
            _context = context;
        }

        /// <inheritdoc/>
        public async Task AddAsync(Player entity, CancellationToken cancellationToken = default)
            => await _context.Players.AddAsync(entity, cancellationToken);

        /// <inheritdoc/>
        public Task<Player?> GetByEmailAsync(string email, CancellationToken cancellationToken = default)
            => _context.Players.FirstOrDefaultAsync(p => p.Email.ToLower() == email.ToLower(), cancellationToken);

        /// <inheritdoc/>
        public Task<Player?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
            => _context.Players.FirstOrDefaultAsync(p => p.Id == id, cancellationToken);

        /// <inheritdoc/>
        public Task<Player?> GetByUserIdAsync(string userId, int serverId, CancellationToken cancellationToken = default)
            => _context.Players
                .FirstOrDefaultAsync(p => p.UserId == userId && p.ServerId == serverId, cancellationToken);

        /// <inheritdoc/>
        public Task<List<Player>> GetAllByUserIdAsync(string userId, CancellationToken cancellationToken = default)
            => _context.Players
                .AsNoTracking()
                .OrderBy(p => p.ServerId)
                .ToListAsync(cancellationToken);
    }
}
