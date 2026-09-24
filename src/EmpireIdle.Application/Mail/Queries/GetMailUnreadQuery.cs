using EmpireIdle.Application.Common.Security;
using EmpireIdle.Application.Interfaces;
using MediatR;

namespace EmpireIdle.Application.Mail.Queries
{
    /// <summary>Лічильник для бейджа: непрочитані листи й оголошення разом (GDD §7.4).</summary>
    public record GetMailUnreadQuery(Guid PlayerId) : IRequest<int>, IPlayerScopedRequest;

    public sealed class GetMailUnreadQueryHandler : IRequestHandler<GetMailUnreadQuery, int>
    {
        private readonly IMailRepository _mail;
        private readonly TimeProvider _timeProvider;

        public GetMailUnreadQueryHandler(IMailRepository mail, TimeProvider timeProvider)
        {
            _mail = mail;
            _timeProvider = timeProvider;
        }

        public async Task<int> Handle(GetMailUnreadQuery request, CancellationToken cancellationToken)
        {
            var now = _timeProvider.GetUtcNow().UtcDateTime;

            return await _mail.CountUnreadLettersAsync(request.PlayerId, now, cancellationToken)
                + await _mail.CountUnreadAnnouncementsAsync(request.PlayerId, now, cancellationToken);
        }
    }
}
