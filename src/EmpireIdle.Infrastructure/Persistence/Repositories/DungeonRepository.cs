using EmpireIdle.Application.Interfaces;
using EmpireIdle.Domain.Entities;
using EmpireIdle.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace EmpireIdle.Infrastructure.Persistence.Repositories
{
    public class DungeonRepository : IDungeonRepository
    {
        private readonly AppDbContext _context;

        public DungeonRepository(AppDbContext context)
        {
            _context = context;
        }

        public Task<DungeonRun?> GetActiveRunAsync(Guid playerId, CancellationToken cancellationToken = default)
            => _context.DungeonRuns
                .FirstOrDefaultAsync(r => r.PlayerId == playerId && r.State == DungeonRunState.InProgress, cancellationToken);

        public Task<DungeonRun?> GetRunByIdAsync(Guid runId, CancellationToken cancellationToken = default)
            => _context.DungeonRuns.FirstOrDefaultAsync(r => r.Id == runId, cancellationToken);

        public async Task AddRunAsync(DungeonRun run, CancellationToken cancellationToken = default)
            => await _context.DungeonRuns.AddAsync(run, cancellationToken);

        public Task<DungeonEnergy?> GetEnergyAsync(Guid playerId, CancellationToken cancellationToken = default)
            => _context.DungeonEnergy.FirstOrDefaultAsync(e => e.PlayerId == playerId, cancellationToken);

        public async Task AddEnergyAsync(DungeonEnergy energy, CancellationToken cancellationToken = default)
            => await _context.DungeonEnergy.AddAsync(energy, cancellationToken);

        public async Task<Dictionary<string, int>> GetClearedLevelsAsync(Guid playerId, CancellationToken cancellationToken = default)
        {
            var rows = await _context.DungeonClears
                .AsNoTracking()
                .Where(c => c.PlayerId == playerId)
                .Select(c => new { c.DungeonKey, c.Level })
                .ToListAsync(cancellationToken);

            return rows
                .GroupBy(r => r.DungeonKey)
                .ToDictionary(g => g.Key, g => g.Max(r => r.Level));
        }

        public async Task AddClearAsync(DungeonClear clear, CancellationToken cancellationToken = default)
            => await _context.DungeonClears.AddAsync(clear, cancellationToken);
    }
}
