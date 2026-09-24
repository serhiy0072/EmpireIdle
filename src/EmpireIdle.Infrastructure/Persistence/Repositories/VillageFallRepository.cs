using EmpireIdle.Application.Interfaces;
using EmpireIdle.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace EmpireIdle.Infrastructure.Persistence.Repositories
{
    public class VillageFallRepository : IVillageFallRepository
    {
        private readonly AppDbContext _context;

        public VillageFallRepository(AppDbContext context)
        {
            _context = context;
        }

        public async Task AddAsync(VillageFall fall, CancellationToken cancellationToken = default)
            => await _context.VillageFalls.AddAsync(fall, cancellationToken);

        public Task<VillageFall?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
            => _context.VillageFalls.AsNoTracking().FirstOrDefaultAsync(f => f.Id == id, cancellationToken);

        public Task<int> CountByAttackerSinceAsync(Guid attackerPlayerId, DateTime since, CancellationToken cancellationToken = default)
            => _context.VillageFalls.CountAsync(f => f.AttackerPlayerId == attackerPlayerId && f.OccurredAt >= since,
                cancellationToken);
    }
}
