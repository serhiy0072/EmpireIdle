using EmpireIdle.Application.Interfaces;
using EmpireIdle.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace EmpireIdle.Infrastructure.Persistence.Repositories
{
    public class PlayerRatingRepository : IPlayerRatingRepository
    {
        private readonly AppDbContext _context;

        public PlayerRatingRepository(AppDbContext context) => _context = context;

        /// <inheritdoc/>
        public Task<PlayerRating?> GetByPlayerAsync(Guid playerId, CancellationToken cancellationToken = default)
            => _context.PlayerRatings.FirstOrDefaultAsync(r => r.PlayerId == playerId, cancellationToken);

        /// <inheritdoc/>
        public Task<List<PlayerRating>> GetTopAsync(int count, CancellationToken cancellationToken = default)
            => _context.PlayerRatings
                .AsNoTracking()
                .OrderByDescending(r => r.TotalRating)
                .ThenBy(r => r.UpdatedAt)
                .Take(count)
                .ToListAsync(cancellationToken);

        /// <inheritdoc/>
        public async Task<int> GetRankAsync(Guid playerId, CancellationToken cancellationToken = default)
        {
            var rating = await _context.PlayerRatings
                .AsNoTracking()
                .Where(r => r.PlayerId == playerId)
                .Select(r => r.TotalRating)
                .FirstOrDefaultAsync(cancellationToken);

            // Скільки гравців стоять вище — позиція на одиницю більша
            var above = await _context.PlayerRatings
                .AsNoTracking()
                .CountAsync(r => r.TotalRating > rating, cancellationToken);

            return above + 1;
        }

        /// <inheritdoc/>
        public async Task AddAsync(PlayerRating rating, CancellationToken cancellationToken = default)
            => await _context.PlayerRatings.AddAsync(rating, cancellationToken);
    }
}
