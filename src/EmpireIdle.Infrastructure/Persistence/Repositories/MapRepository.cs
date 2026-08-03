using EmpireIdle.Application.Interfaces;
using EmpireIdle.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace EmpireIdle.Infrastructure.Persistence.Repositories
{
    /// <summary>Репозиторій карти (EF Core).</summary>
    public class MapRepository : IMapRepository
    {
        private readonly AppDbContext _context;

        public MapRepository(AppDbContext context)
        {
            _context = context;
        }

        /// <inheritdoc/>
        public Task<bool> IsOccupiedAsync(int serverId, int x, int y, CancellationToken cancellationToken = default)  
            => _context.MapCells.AsNoTracking().AnyAsync(c => c.ServerId == serverId && c.X == x && c.Y == y, cancellationToken);


        /// <inheritdoc/>
        public Task<List<MapCell>> GetAreaAsync(int serverId, int minX, int minY, int maxX, int maxY, CancellationToken cancellationToken = default)
            => _context.MapCells
            .AsNoTracking()
            .Where(c => c.ServerId == serverId
                     && c.X >= minX && c.X <= maxX
                     && c.Y >= minY && c.Y <= maxY)
            .ToListAsync(cancellationToken);

        /// <inheritdoc/>
        public Task<MapCell?> GetByOccupantAsync (MapOccupantType occupantType, Guid occupantId, CancellationToken cancellationToken = default)
            => _context.MapCells.AsNoTracking().FirstOrDefaultAsync(c => c.OccupantType == occupantType && c.OccupantId == occupantId, cancellationToken);

        /// <inheritdoc/>
        public async Task AddAsync(MapCell cell, CancellationToken cancellationToken = default)
        {
            await _context.MapCells.AddAsync(cell);
        }
    }
}
