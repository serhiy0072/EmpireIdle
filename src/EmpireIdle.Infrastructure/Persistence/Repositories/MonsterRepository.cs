using EmpireIdle.Application.Interfaces;
using EmpireIdle.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace EmpireIdle.Infrastructure.Persistence.Repositories
{
    /// <summary>Репозиторій монстрів (EF Core).</summary>
    public class MonsterRepository : IMonsterRepository
    {
        private readonly AppDbContext _context;

        public MonsterRepository(AppDbContext context)
        {
            _context = context;
        }

        /// <inheritdoc/>
        public Task<int> CountAsync(int serverId, CancellationToken cancellationToken = default)
            => _context.Monsters.CountAsync(m => m.ServerId == serverId, cancellationToken);

        /// <inheritdoc/>
        public Task<Monster?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
            => _context.Monsters.FirstOrDefaultAsync(m => m.Id == id, cancellationToken);

        /// <inheritdoc/>
        public Task<List<Monster>> GetByIdsAsync(IReadOnlyCollection<Guid> ids, CancellationToken cancellationToken = default)
            => _context.Monsters.AsNoTracking().Where(m => ids.Contains(m.Id)).ToListAsync(cancellationToken);

        public async Task AddAsync(Monster monster, CancellationToken cancellationToken = default)
        {
            await _context.Monsters.AddAsync(monster, cancellationToken);
        }

        /// <inheritdoc/>
        public void Remove(Monster monster) => _context.Monsters.Remove(monster);
    }
}