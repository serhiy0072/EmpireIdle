using EmpireIdle.Application.Interfaces;
using EmpireIdle.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace EmpireIdle.Infrastructure.Persistence.Repositories
{
    public class ServerRepository : IServerRepository
    {
        private readonly AppDbContext _context;

        public ServerRepository(AppDbContext context) => _context = context;

        /// <inheritdoc/>
        public Task<Server?> GetByIdAsync(int id, CancellationToken cancellationToken = default)
            => _context.Servers.FirstOrDefaultAsync(s => s.Id == id, cancellationToken);

        /// <inheritdoc/>
        public async Task<int> GetLevelAsync(int id, CancellationToken cancellationToken = default)
            => await _context.Servers
                .AsNoTracking()
                .Where(s => s.Id == id)
                .Select(s => s.Level)
                .FirstOrDefaultAsync(cancellationToken);

        /// <inheritdoc/>
        public Task<List<Server>> GetAcceptingAsync(CancellationToken cancellationToken = default)
            => _context.Servers
                .AsNoTracking()
                .Where(s => s.State == ServerState.Active)
                .OrderBy(s => s.Id)
                .ToListAsync(cancellationToken);

        /// <inheritdoc/>
        public async Task AddAsync(Server server, CancellationToken cancellationToken = default)
            => await _context.Servers.AddAsync(server, cancellationToken);
    }
}
