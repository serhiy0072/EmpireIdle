using EmpireIdle.Application.Interfaces;
using EmpireIdle.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace EmpireIdle.Infrastructure.Persistence.Repositories
{
    internal class BannerRepository : IBannerRepository
    {
        private readonly AppDbContext _context;

        public BannerRepository(AppDbContext context)
        {
            _context = context;
        }

        /// <inheritdoc/>
        public Task<BannerPityProgress?> GetPityAsync(Guid playerId, string pityGroup, CancellationToken cancellationToken = default)
            => _context.BannerPity
                .FirstOrDefaultAsync(p => p.PlayerId == playerId && p.PityGroup == pityGroup, cancellationToken);

        /// <inheritdoc/>
        public async Task AddPityAsync(BannerPityProgress progress, CancellationToken cancellationToken = default)
        {
            await _context.BannerPity.AddAsync(progress, cancellationToken);
        }

        /// <inheritdoc/>
        public async Task AddRollAsync(BannerRollRecord record, CancellationToken cancellationToken = default)
        {
            await _context.BannerRolls.AddAsync(record, cancellationToken);
        }
    }
}
