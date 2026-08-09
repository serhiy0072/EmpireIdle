using EmpireIdle.Application.Interfaces;
using EmpireIdle.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace EmpireIdle.Infrastructure.Persistence.Repositories
{
    /// <summary>Репозиторій записів ідемпотентності (EF Core).</summary>
    public class IdempotencyRepository : IIdempotencyRepository
    {
        private readonly AppDbContext _context;

        public IdempotencyRepository(AppDbContext context)
        {
            _context = context;
        }

        /// <inheritdoc/>
        public Task<IdempotencyRecord?> FindAsync(Guid playerId, string key, CancellationToken cancellationToken = default)
            => _context.IdempotencyRecords
            .AsNoTracking()
            .FirstOrDefaultAsync(r => r.PlayerId == playerId && r.Key == key, cancellationToken);

        /// <inheritdoc/>
        public async Task AddAsync(IdempotencyRecord record, CancellationToken cancellationToken = default)
        {
            await _context.IdempotencyRecords.AddAsync(record, cancellationToken);
        }
    }
}