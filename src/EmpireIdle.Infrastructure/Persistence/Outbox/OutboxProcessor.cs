using EmpireIdle.Application.Common.Events;
using EmpireIdle.Application.Interfaces;
using EmpireIdle.Domain.Events;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System.Text.Json;

namespace EmpireIdle.Infrastructure.Persistence.Outbox
{
    /// <summary>
    /// Читає незавершені події з Outbox і публікує через MediatR.
    /// Кожне повідомлення — свій scope і своя транзакція: помилка в Postgres
    /// перериває транзакцію цілком, тож спільний batch зіпсував би решту.
    /// </summary>
    public class OutboxProcessor : BackgroundService
    {
        private static readonly Dictionary<string, Type> EventTypes =
            typeof(IDomainEvent).Assembly
            .GetTypes()
            .Where(t => typeof(IDomainEvent).IsAssignableFrom(t) && !t.IsAbstract && !t.IsInterface)
            .ToDictionary(t => t.FullName!);

        private readonly IServiceScopeFactory _scopeFactory;
        private readonly OutboxSettings _settings;
        private readonly ILogger<OutboxProcessor> _logger;

        public OutboxProcessor(IServiceScopeFactory scopeFactory, IOptions<OutboxSettings> settings, ILogger<OutboxProcessor> logger)
        {
            _scopeFactory = scopeFactory;
            _settings = settings.Value;
            _logger = logger;
        }

        protected override async Task ExecuteAsync(CancellationToken cancellationToken)
        {
            var delay = TimeSpan.FromSeconds(_settings.PollSeconds);

            while (!cancellationToken.IsCancellationRequested)
            {
                try
                {
                    var processed = await ProcessBatchAsync(cancellationToken);

                    // Черга не порожня — не спимо, розбираємо далі
                    if (processed == _settings.BatchSize)
                        continue;
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Outbox batch failed.");
                }
                await Task.Delay(delay, cancellationToken);
            }
        }

        private async Task<int> ProcessBatchAsync(CancellationToken cancellationToken)
        {
            List<Guid> ids;

            using (var scope = _scopeFactory.CreateScope())
            {
                var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();
                ids = await context.OutboxMessages
                    .AsNoTracking()
                    .Where(m => m.ProcessedAt == null && m.Attempts < _settings.MaxAttempts)
                    .OrderBy(m => m.OccurredAt)
                    .Take(_settings.BatchSize)
                    .Select(m => m.Id)
                    .ToListAsync(cancellationToken);
            }

            var processed = 0;

            foreach (var id in ids)
            {
                if (await ProcessesOneAsync(id, cancellationToken))
                    processed++;
            }

            return processed;
        }

        private async Task<bool> ProcessesOneAsync(Guid id, CancellationToken cancellationToken)
        {
            using var scope = _scopeFactory.CreateScope();
            var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var publisher = scope.ServiceProvider.GetRequiredService<IPublisher>();

            await using var transaction = await context.Database.BeginTransactionAsync(cancellationToken);

            // SKIP LOCKED: паралельні інстанси не візьмуть те саме повідомлення
            var message = await context.OutboxMessages
                .FromSqlRaw("""
                    SELECT * FROM "OutboxMessages"
                    WHERE "Id" = {0} AND "ProcessedAt" IS NULL
                    FOR UPDATE SKIP LOCKED
                    """, id)
                .FirstOrDefaultAsync(cancellationToken);

            if (message is null)
                return false;

            // Підписники читатимуть відфільтровані сутності — світ має бути той самий,
            // у якому подія сталася, а не той, у якому крутиться воркер
            scope.ServiceProvider.GetRequiredService<IServerContext>().UseServer(message.ServerId);

            try
            {
                if (!EventTypes.TryGetValue(message.Type, out var eventType))
                    throw new InvalidOperationException($"Unknown domain event type '{message.Type}'.");

                var domainEvent = (IDomainEvent)JsonSerializer.Deserialize(message.Payload, eventType)!;

                var notificationType = typeof(DomainEventNotification<>).MakeGenericType(eventType);
                var notification = (INotification)Activator.CreateInstance(notificationType, domainEvent)!;

                // Хендлери працюють у тому ж контексті, отже їхні зміни
                // комітяться разом із позначкою "оброблено"
                await publisher.Publish(notification, cancellationToken);

                message.ProcessedAt = DateTime.UtcNow;
                await context.SaveChangesAsync(cancellationToken);
                await transaction.CommitAsync(cancellationToken);

                return true;
            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync(cancellationToken);

                _logger.LogError(ex, "Failed to publish outbox message {MessageId} of type {Type}", id, message.Type);
                await RecordFailureAsync(id, ex.Message, cancellationToken);

                return false;
            }
        }


        /// <summary>Пише лічильник спроб окремою транзакцією — попередня вже відкотилась.</summary>
        private async Task RecordFailureAsync(Guid id, string error, CancellationToken cancellationToken)
        {
            using var scope = _scopeFactory.CreateScope();
            var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();

            await context.OutboxMessages
                .Where(m => m.Id == id)
                .ExecuteUpdateAsync(s => s
                    .SetProperty(m => m.Attempts, m => m.Attempts + 1)
                    .SetProperty(m => m.Error, error.Length > 2000 ? error[..2000] : error),
                    cancellationToken);
        }
    }
}
