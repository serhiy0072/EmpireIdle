using EmpireIdle.Application.Interfaces;
using EmpireIdle.Domain.Entities;
using EmpireIdle.Infrastructure.Persistence.Outbox;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using System.Text.Json;

namespace EmpireIdle.Infrastructure.Persistence.Interceptors
{
    /// <summary>
    /// Складає доменні події в Outbox ПЕРЕД збереженням — тобто в ту саму транзакцію,
    /// що й зміна стану. Публікує їх окремий воркер.
    /// </summary>
    public class DomainEventDispatchInterceptor : SaveChangesInterceptor
    {
        private readonly IServerContext _serverContext;
        private readonly TimeProvider _timeProvider;

        public DomainEventDispatchInterceptor(IServerContext serverContext, TimeProvider timeProvider)
        {
            _serverContext = serverContext;
            _timeProvider = timeProvider;
        }

        public override ValueTask<InterceptionResult<int>> SavingChangesAsync(
            DbContextEventData eventData,
            InterceptionResult<int> result,
            CancellationToken cancellationToken = default)
        {
            if (eventData.Context is not null)
                WriteToOutbox(eventData.Context);

            return base.SavingChangesAsync(eventData, result, cancellationToken);
        }

        private void WriteToOutbox(DbContext context)
        {
            var entitiesWithEvents = context.ChangeTracker
                .Entries<Entity>()
                .Where(e => e.Entity.DomainEvents.Count > 0)
                .Select(e => e.Entity)
                .ToList();

            if (entitiesWithEvents.Count == 0)
                return;

            // Читаємо ЛИШЕ коли є що писати: у scope без подій (наприклад,
            // позначка ProcessedAt у воркері) сервер може бути не встановлений
            var serverId = _serverContext.ServerId;
            var utcNow = _timeProvider.GetUtcNow().UtcDateTime;

            var messages = entitiesWithEvents
                .SelectMany(e => e.DomainEvents)
                .Select(domainEvent => new OutboxMessage
                {
                    Id = Guid.NewGuid(),
                    ServerId = serverId,
                    Type = domainEvent.GetType().FullName!,
                    Payload = JsonSerializer.Serialize(domainEvent, domainEvent.GetType()),
                    OccurredAt = utcNow
                })
                .ToList();

            // Очищуємо до збереження: подія вже зафіксована в Outbox,
            // повторний SaveChanges не має додати її вдруге
            foreach (var entity in entitiesWithEvents)
                entity.ClearDomainEvents();

            context.Set<OutboxMessage>().AddRange(messages);
        }
    }
}
