using EmpireIdle.Application.Interfaces;
using EmpireIdle.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace EmpireIdle.Infrastructure.Persistence.Repositories
{
    public class PlayerPowerRepository : IPlayerPowerRepository
    {
        private readonly AppDbContext _context;

        public PlayerPowerRepository(AppDbContext context) => _context = context;

        /// <inheritdoc/>
        public Task<PlayerPower?> GetByPlayerAsync(Guid playerId, CancellationToken cancellationToken = default)
            => _context.PlayerPowers.FirstOrDefaultAsync(p => p.PlayerId == playerId, cancellationToken);

        /// <inheritdoc/>
        public async Task AddAsync(PlayerPower power, CancellationToken cancellationToken = default)
            => await _context.PlayerPowers.AddAsync(power, cancellationToken);

        /// <inheritdoc/>
        public Task<List<PlayerPower>> GetAllAsync(CancellationToken cancellationToken = default)
            => _context.PlayerPowers.AsNoTracking().ToListAsync(cancellationToken);

        /// <inheritdoc/>
        public Task<Dictionary<Guid, double>> GetTotalPowerAsync(IReadOnlyCollection<Guid> playerIds,
            CancellationToken cancellationToken = default)
            => _context.PlayerPowers
                .AsNoTracking()
                .Where(p => playerIds.Contains(p.PlayerId))
                .ToDictionaryAsync(p => p.PlayerId, p => p.TotalPower, cancellationToken);
    }
}
