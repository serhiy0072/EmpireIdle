using EmpireIdle.Application.Common.Events;
using EmpireIdle.Application.Interfaces;
using EmpireIdle.Domain.Events;
using MediatR;

namespace EmpireIdle.Application.Rating.Tracking
{
    /// <summary>
    /// Лічильники активності. Ростуть подіями й не перераховуються з нуля:
    /// джерела для перерахунку немає — бій не лишає рядка, який можна
    /// перечитати. Пропущена подія коштує невеликого недоліку назавжди,
    /// і це прийнятно: лічильники монотонні й не розганяють похибку.
    ///
    /// Підсумковий рейтинг вони не чіпають — його раз на годину перераховує
    /// RatingRecalculationJob із поточної сили й рівнів будівель.
    /// </summary>
    public sealed class RecordMonsterDefeated
        : INotificationHandler<DomainEventNotification<MonsterDefeated>>
    {
        private readonly IPlayerRatingRepository _ratings;
        private readonly IUnitOfWork _unitOfWork;

        public RecordMonsterDefeated(IPlayerRatingRepository ratings, IUnitOfWork unitOfWork)
        {
            _ratings = ratings;
            _unitOfWork = unitOfWork;
        }

        public async Task Handle(DomainEventNotification<MonsterDefeated> notification,
            CancellationToken cancellationToken)
        {
            var rating = await _ratings.GetByPlayerAsync(notification.DomainEvent.PlayerId, cancellationToken);

            // Рядка ще немає — його створить найближчий прогін джоба,
            // і лічильник почнеться з наступної події
            if (rating is null)
                return;

            rating.RecordActivity(monsters: 1);

            await _unitOfWork.SaveChangesAsync(cancellationToken);
        }
    }

    /// <inheritdoc cref="RecordMonsterDefeated"/>
    public sealed class RecordBattleFought
        : INotificationHandler<DomainEventNotification<BattleFought>>
    {
        private readonly IPlayerRatingRepository _ratings;
        private readonly IUnitOfWork _unitOfWork;

        public RecordBattleFought(IPlayerRatingRepository ratings, IUnitOfWork unitOfWork)
        {
            _ratings = ratings;
            _unitOfWork = unitOfWork;
        }

        public async Task Handle(DomainEventNotification<BattleFought> notification,
            CancellationToken cancellationToken)
        {
            // Поразка активності не додає: рейтинг міряє досягнення,
            // а не кількість спроб
            if (!notification.DomainEvent.Won)
                return;

            var rating = await _ratings.GetByPlayerAsync(notification.DomainEvent.PlayerId, cancellationToken);

            if (rating is null)
                return;

            rating.RecordActivity(battlesWon: 1);

            await _unitOfWork.SaveChangesAsync(cancellationToken);
        }
    }

    /// <inheritdoc cref="RecordMonsterDefeated"/>
    public sealed class RecordQuestCompleted
        : INotificationHandler<DomainEventNotification<QuestCompleted>>
    {
        private readonly IPlayerRatingRepository _ratings;
        private readonly IUnitOfWork _unitOfWork;

        public RecordQuestCompleted(IPlayerRatingRepository ratings, IUnitOfWork unitOfWork)
        {
            _ratings = ratings;
            _unitOfWork = unitOfWork;
        }

        public async Task Handle(DomainEventNotification<QuestCompleted> notification,
            CancellationToken cancellationToken)
        {
            var rating = await _ratings.GetByPlayerAsync(notification.DomainEvent.PlayerId, cancellationToken);

            if (rating is null)
                return;

            rating.RecordActivity(quests: 1);

            await _unitOfWork.SaveChangesAsync(cancellationToken);
        }
    }
}
