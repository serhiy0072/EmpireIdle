using EmpireIdle.Application.Common.Events;
using EmpireIdle.Application.Interfaces;
using EmpireIdle.Domain.Events;
using MediatR;

namespace EmpireIdle.Application.Clans.EventHandlers
{
    /// <summary>
    /// Апгрейд завершився — запит допомоги на нього більше нічого не важить. Прибираємо
    /// одразу, а не чекаємо ExpiresAt: прискорений апгрейд закінчується раніше, і Building.Id
    /// той самий, тож наступний апгрейд інакше отримав би «допомогу вже запитано».
    /// </summary>
    public sealed class ClearHelpOnUpgradeCompletedHandler
        : INotificationHandler<DomainEventNotification<BuildingUpgradeCompleted>>
    {
        private readonly IClanHelpRepository _helpRepository;

        public ClearHelpOnUpgradeCompletedHandler(IClanHelpRepository helpRepository) => _helpRepository = helpRepository;

        public Task Handle(DomainEventNotification<BuildingUpgradeCompleted> notification, CancellationToken cancellationToken)
            => _helpRepository.RemoveForTargetAsync(notification.DomainEvent.BuildingId, cancellationToken: cancellationToken);
    }
}
