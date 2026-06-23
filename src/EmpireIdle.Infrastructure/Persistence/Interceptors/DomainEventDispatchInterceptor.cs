using EmpireIdle.Application.Common.Events;
using EmpireIdle.Domain.Entities;
using EmpireIdle.Domain.Events;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace EmpireIdle.Infrastructure.Persistence.Interceptors
{
    /// <summary>
    /// EF Core interceptor, що публікує доменні події ПІСЛЯ успішного збереження змін.
    /// Збирає події з усіх трекнутих агрегатів, очищує їх і публікує через MediatR.
    /// </summary>
    public class DomainEventDispatchInterceptor : SaveChangesInterceptor
    {
        private readonly IPublisher _publisher;

        public DomainEventDispatchInterceptor(IPublisher publisher)
        {
            _publisher = publisher; 
        }

        public override async ValueTask<int> SavedChangesAsync(SaveChangesCompletedEventData eventData, int result, CancellationToken cancellationToken = default)
        {
            if (eventData.Context is not null)
                await DispatchDomainEventsAsync(eventData.Context, cancellationToken);

            return await base.SavedChangesAsync(eventData, result, cancellationToken);
        }

        private async Task DispatchDomainEventsAsync(DbContext context, CancellationToken cancellationToken)
        {
            var entitiesWithEvents = context.ChangeTracker
                .Entries<Entity>()
                .Where(e => e.Entity.DomainEvents.Count > 0)
                .Select(e => e.Entity)
                .ToList();

            var domainEvents = entitiesWithEvents
                .SelectMany(e => e.DomainEvents)
                .ToList();

            // Спершу очищуємо, потім публікуємо: якщо handler знову викличе SaveChanges,
            // ці ж події не опублікуються вдруге.
            foreach (var entity in entitiesWithEvents)
                entity.ClearDomainEvents();

            foreach (var domainEvent in domainEvents)
                await _publisher.Publish(CreateNotification(domainEvent), cancellationToken);
        }

        private static INotification CreateNotification(IDomainEvent domainEvent)
        {
            var notificationType = typeof(DomainEventNotification<>).MakeGenericType(domainEvent.GetType());
            return (INotification)Activator.CreateInstance(notificationType, domainEvent)!;
        }
    }
}
