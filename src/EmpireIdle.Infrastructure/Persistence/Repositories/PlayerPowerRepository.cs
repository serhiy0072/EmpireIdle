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
    }
}
