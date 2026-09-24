using EmpireIdle.Application.Common.Security;
using EmpireIdle.Application.Interfaces;
using EmpireIdle.Domain.Entities;
using EmpireIdle.Domain.Exceptions;
using MediatR;

namespace EmpireIdle.Application.Mail.Commands
{
    /// <summary>
    /// Позначити оголошення прочитаним. Мітка створюється лише тепер — оголошення
    /// не розмножується по адресатах (GDD §7.4). Повторна позначка нічого не змінює.
    /// </summary>
    public record MarkAnnouncementReadCommand(Guid PlayerId, Guid AnnouncementId)
        : IRequest, IPlayerScopedRequest, IIdempotentRequest;

    public sealed class MarkAnnouncementReadCommandHandler : IRequestHandler<MarkAnnouncementReadCommand>
    {
        private readonly IMailRepository _mail;
        private readonly IUnitOfWork _unitOfWork;
        private readonly TimeProvider _timeProvider;

        public MarkAnnouncementReadCommandHandler(IMailRepository mail, IUnitOfWork unitOfWork, TimeProvider timeProvider)
        {
            _mail = mail;
            _unitOfWork = unitOfWork;
            _timeProvider = timeProvider;
        }

        public async Task Handle(MarkAnnouncementReadCommand request, CancellationToken cancellationToken)
        {
            _ = await _mail.GetAnnouncementAsync(request.AnnouncementId, cancellationToken)
                ?? throw new EntityNotFoundException("Announcement", request.AnnouncementId.ToString());

            if (await _mail.IsAnnouncementReadAsync(request.AnnouncementId, request.PlayerId, cancellationToken))
                return;

            await _mail.AddAnnouncementReadAsync(
                new AnnouncementRead(request.AnnouncementId, request.PlayerId, _timeProvider.GetUtcNow().UtcDateTime),
                cancellationToken);

            await _unitOfWork.SaveChangesAsync(cancellationToken);
        }
    }
}
