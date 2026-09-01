using EmpireIdle.Application.Common.Events;
using EmpireIdle.Application.Interfaces;
using EmpireIdle.Domain.Events;
using MediatR;

namespace EmpireIdle.Application.ServerQuests.EventHandlers
{
    /// <summary>Надсилає гравцю realtime-сповіщення про нагороду за серверний квест.</summary>
    public sealed class ServerQuestRewardedNotificationHandler
        : INotificationHandler<DomainEventNotification<ServerQuestRewarded>>
    {
        private readonly IGameNotifier _notifier;

        public ServerQuestRewardedNotificationHandler(IGameNotifier notifier) => _notifier = notifier;

        public Task Handle(DomainEventNotification<ServerQuestRewarded> notification,
            CancellationToken cancellationToken)
        {
            var e = notification.DomainEvent;

            return _notifier.NotifyServerQuestRewardedAsync(
                e.PlayerId, e.QuestKey, e.Rank, e.Contribution, cancellationToken);
        }
    }
}
