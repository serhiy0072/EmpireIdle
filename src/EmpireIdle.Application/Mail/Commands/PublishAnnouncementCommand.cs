using EmpireIdle.Application.Interfaces;
using EmpireIdle.Domain.Entities;
using EmpireIdle.Domain.Enums;
using EmpireIdle.Domain.Services;
using MediatR;

namespace EmpireIdle.Application.Mail.Commands
{
    /// <summary>
    /// Опублікувати оголошення в поточному світі. Системна команда: адмінського
    /// інструменту ще немає, у розробці її викликає dev-маршрут.
    /// </summary>
    /// <param name="ExpiresAt">null — строк за замовчуванням із конфіга.</param>
    public record PublishAnnouncementCommand(AnnouncementKind Kind, string Title, string Body, DateTime? ExpiresAt)
        : IRequest<Guid>;

    public sealed class PublishAnnouncementCommandHandler : IRequestHandler<PublishAnnouncementCommand, Guid>
    {
        private readonly IMailRepository _mail;
        private readonly IServerContext _serverContext;
        private readonly IUnitOfWork _unitOfWork;
        private readonly IGameNotifier _notifier;
        private readonly GameCatalog _catalog;
        private readonly TimeProvider _timeProvider;

        public PublishAnnouncementCommandHandler(IMailRepository mail, IServerContext serverContext, IUnitOfWork unitOfWork,
            IGameNotifier notifier, GameCatalog catalog, TimeProvider timeProvider)
        {
            _mail = mail;
            _serverContext = serverContext;
            _unitOfWork = unitOfWork;
            _notifier = notifier;
            _catalog = catalog;
            _timeProvider = timeProvider;
        }

        public async Task<Guid> Handle(PublishAnnouncementCommand request, CancellationToken cancellationToken)
        {
            var now = _timeProvider.GetUtcNow().UtcDateTime;
            var expiresAt = request.ExpiresAt ?? now.AddDays(_catalog.Config.Mail.AnnouncementRetentionDays);

            var announcement = new Announcement(Guid.NewGuid(), _serverContext.ServerId, request.Kind, request.Title.Trim(),
                request.Body.Trim(), now, expiresAt);

            await _mail.AddAnnouncementAsync(announcement, cancellationToken);
            await _unitOfWork.SaveChangesAsync(cancellationToken);

            await _notifier.NotifyAnnouncementAsync(_serverContext.ServerId, cancellationToken);

            return announcement.Id;
        }
    }
}
