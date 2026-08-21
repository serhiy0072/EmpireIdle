using EmpireIdle.Application.Interfaces;
using EmpireIdle.Infrastructure.Persistence;
using EmpireIdle.Infrastructure.Persistence.Outbox;
using Hangfire;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace EmpireIdle.API.Jobs
{
    /// <summary>
    /// Прибирає оброблені події та сигналить про ті, що вичерпали спроби.
    /// Без цього таблиця росте нескінченно, а «отруєні» повідомлення нікому не видно.
    /// </summary>
    public class OutboxMaintenanceJob
    {
        private readonly AppDbContext _context;
        private readonly OutboxSettings _settings;
        private readonly IIdempotencyRepository _idempotency;
        private readonly ILogger<OutboxMaintenanceJob> _logger;

        public OutboxMaintenanceJob(
            AppDbContext context,
            IOptions<OutboxSettings> settings,
            IIdempotencyRepository idempotency,
            ILogger<OutboxMaintenanceJob> logger)
        {
            _context = context;
            _settings = settings.Value;
            _idempotency = idempotency;
            _logger = logger;
        }

        [DisableConcurrentExecution(timeoutInSeconds: 300)]
        public async Task RunAsync()
        {
            var now = DateTime.UtcNow;
            var cutoff = now.AddDays(-_settings.RetentionDays);
            var staleCutoff = now.AddHours(-_settings.StaleReservationHours);

            var deleted = await _context.OutboxMessages
                .IgnoreQueryFilters()
                .Where(m => m.ProcessedAt != null && m.ProcessedAt < cutoff)
                .ExecuteDeleteAsync();

            var poisoned = await _context.OutboxMessages
                .IgnoreQueryFilters()
                .Where(m => m.ProcessedAt == null && m.Attempts >= _settings.MaxAttempts)
                .GroupBy(m => m.Type)
                .Select(g => new { Type = g.Key, Count = g.Count() })
                .ToListAsync();

            var releasedKeys = await _idempotency.PurgeStaleReservationsAsync(staleCutoff);

            if (deleted > 0)
                _logger.LogInformation("Outbox cleanup removed {Deleted} processed messages.", deleted);

            foreach (var group in poisoned)
                _logger.LogError("Outbox has {Count} poisoned messages of type {Type} — needs manual review.", group.Count, group.Type);

            if (releasedKeys > 0)
                _logger.LogWarning("Released {Count} stale idempotency reservations.", releasedKeys);
        }
    }
}
