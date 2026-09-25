using EmpireIdle.Application.Interfaces;
using EmpireIdle.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace EmpireIdle.Infrastructure.Persistence.Repositories
{
    public class ClanStructureRepository : IClanStructureRepository
    {
        private readonly AppDbContext _context;

        public ClanStructureRepository(AppDbContext context) => _context = context;

        /// <inheritdoc/>
        public Task<ClanStructure?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
            => _context.ClanStructures.FirstOrDefaultAsync(s => s.Id == id, cancellationToken);

        /// <inheritdoc/>
        public Task<List<ClanStructure>> GetByClanAsync(Guid clanId, CancellationToken cancellationToken = default)
            => _context.ClanStructures.Where(s => s.ClanId == clanId).ToListAsync(cancellationToken);

        /// <inheritdoc/>
        public Task<List<ClanStructure>> GetInAreaAsync(int minX, int minY, int maxX, int maxY,
            CancellationToken cancellationToken = default)
            => _context.ClanStructures
                .Where(s => s.X >= minX && s.X <= maxX && s.Y >= minY && s.Y <= maxY)
                .ToListAsync(cancellationToken);

        /// <inheritdoc/>
        public async Task AddAsync(ClanStructure structure, CancellationToken cancellationToken = default)
            => await _context.ClanStructures.AddAsync(structure, cancellationToken);

        /// <inheritdoc/>
        public void Remove(ClanStructure structure) => _context.ClanStructures.Remove(structure);
    }
}
