using EmpireIdle.Application.Interfaces;
using EmpireIdle.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Npgsql;

namespace EmpireIdle.Infrastructure.Persistence.Repositories
{
    /// <summary>Репозиторій записів ідемпотентності (EF Core).</summary>
    public class IdempotencyRepository : IIdempotencyRepository
    {
        private readonly AppDbContext _context;
        private readonly IDbContextFactory<AppDbContext> _contextFactory;
        private readonly ILogger<IdempotencyRepository> _logger;

        public IdempotencyRepository(
            AppDbContext context,
            IDbContextFactory<AppDbContext> contextFactory,
            ILogger<IdempotencyRepository> logger)
        {
            _context = context;
            _contextFactory = contextFactory;
            _logger = logger;
        }

        public Task<IdempotencyRecord?> FindAsync(Guid playerId, string key, CancellationToken cancellationToken = default)
            => _context.IdempotencyRecords
                .AsNoTracking()
                .FirstOrDefaultAsync(r => r.PlayerId == playerId && r.Key == key, cancellationToken);

        public async Task<bool> TryReserveAsync(IdempotencyRecord record, CancellationToken cancellationToken = default)
        {
            // Окремий контекст: резерв має жити незалежно від транзакції самої операції.
            // Інакше відкат операції зніс би і резерв — і два паралельні запити пройшли б обидва.
            await using var scoped = await _contextFactory.CreateDbContextAsync(cancellationToken);

            scoped.IdempotencyRecords.Add(record);

            try
            {
                await scoped.SaveChangesAsync(cancellationToken);
                return true;
            }
            catch (DbUpdateException ex) when (ex.InnerException is PostgresException { SqlState: "23505" })
            {
                _logger.LogInformation("Idempotency key {Key} already reserved for player {PlayerId}.",
                    record.Key, record.PlayerId);
                return false;
            }
        }

        public async Task ReleaseAsync(Guid recordId, CancellationToken cancellationToken = default)
        {
            await using var scoped = await _contextFactory.CreateDbContextAsync(cancellationToken);

            await scoped.IdempotencyRecords
                .Where(r => r.Id == recordId)
                .ExecuteDeleteAsync(cancellationToken);
        }

        /// <inheritdoc/>
        public async Task CompleteAsync(Guid recordId, string? responseJson, CancellationToken cancellationToken = default)
        {
            // Окремий контекст — той самий шлях, що й резервував. Запис не трекається
            // scoped-контекстом, тому SaveChanges на UnitOfWork його не бачить.
            await using var scoped = await _contextFactory.CreateDbContextAsync(cancellationToken);

            var affected = await scoped.IdempotencyRecords
                .Where(r => r.Id == recordId)
                .ExecuteUpdateAsync(
                    s => s.SetProperty(r => r.ResponseJson, responseJson),
                    cancellationToken);

            if (affected == 0)
                _logger.LogWarning("Idempotency record {RecordId} vanished before its response was stored.", recordId);
        }

        /// <inheritdoc/>
        public async Task<int> PurgeStaleReservationsAsync(DateTime cutoffUtc, CancellationToken cancellationToken = default)
        {
            await using var scoped = await _contextFactory.CreateDbContextAsync(cancellationToken);

            return await scoped.IdempotencyRecords
                .Where(r => r.ResponseJson == null && r.CreatedAt < cutoffUtc)
                .ExecuteDeleteAsync(cancellationToken);
        }
    }
}
