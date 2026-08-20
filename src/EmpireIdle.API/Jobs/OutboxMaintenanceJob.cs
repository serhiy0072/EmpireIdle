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
        private readonly ILogger<OutboxMaintenanceJob> _logger;

        public OutboxMaintenanceJob(
            AppDbContext context,
            IOptions<OutboxSettings> settings,
            ILogger<OutboxMaintenanceJob> logger)
        {
            _context = context;
            _settings = settings.Value;
            _logger = logger;
        }

        [DisableConcurrentExecution(timeoutInSeconds: 300)]
        public async Task RunAsync()
        {
            var cutoff = DateTime.UtcNow.AddDays(-_settings.RetentionDays);

            var deleted = await _context.OutboxMessages
                .IgnoreQueryFilters()
                .Where(m => m.ProcessedAt != null && m.ProcessedAt < cutoff)
                .ExecuteDeleteAsync();

            // Не видаляємо: це діагностика реальної поломки, її треба розібрати руками
            var poisoned = await _context.OutboxMessages
                .Where(m => m.ProcessedAt == null && m.Attempts >= _settings.MaxAttempts)
                .GroupBy(m => m.Type)
                .Select(g => new { Type = g.Key, Count = g.Count() })
                .ToListAsync();

            foreach (var group in poisoned)
                _logger.LogError("Outbox has {Count} poisoned messages of type {Type} — needs manual review.",
                    group.Count, group.Type);

            if (deleted > 0)
                _logger.LogInformation("Outbox cleanup removed {Deleted} processed messages.", deleted);
        }
    }
}
