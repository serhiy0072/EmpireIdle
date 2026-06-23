using EmpireIdle.Domain.Events;
using MediatR;

namespace EmpireIdle.Application.Common.Events
{
    /// <summary>
    /// Обгортка, що адаптує доменну подію (IDomainEvent) до MediatR INotification.
    /// Дозволяє публікувати доменні події через IPublisher, тримаючи шар Domain
    /// незалежним від MediatR.
    /// </summary>
    /// /// <typeparam name="TDomainEvent">Конкретний тип доменної події.</typeparam>
    public record DomainEventNotification<TDomainEvent>(TDomainEvent DomainEvent) : INotification
        where TDomainEvent : IDomainEvent;
}
