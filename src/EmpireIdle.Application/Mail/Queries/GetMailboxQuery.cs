using EmpireIdle.Application.Common.Security;
using EmpireIdle.Application.Interfaces;
using EmpireIdle.Application.Mail.Contracts;
using EmpireIdle.Domain.Entities;
using EmpireIdle.Domain.Enums;
using MediatR;

namespace EmpireIdle.Application.Mail.Queries
{
    /// <summary>Скринька гравця з актуальним станом того, на що посилаються листи.</summary>
    public record GetMailboxQuery(Guid PlayerId) : IRequest<MailboxView>, IPlayerScopedRequest;

    public sealed class GetMailboxQueryHandler : IRequestHandler<GetMailboxQuery, MailboxView>
    {
        private readonly IMailRepository _mail;
        private readonly IClanRequestRepository _requests;
        private readonly IClanRepository _clans;
        private readonly TimeProvider _timeProvider;

        public GetMailboxQueryHandler(IMailRepository mail, IClanRequestRepository requests, IClanRepository clans,
            TimeProvider timeProvider)
        {
            _mail = mail;
            _requests = requests;
            _clans = clans;
            _timeProvider = timeProvider;
        }

        public async Task<MailboxView> Handle(GetMailboxQuery request, CancellationToken cancellationToken)
        {
            var now = _timeProvider.GetUtcNow().UtcDateTime;

            var letters = await _mail.GetLettersAsync(request.PlayerId, now, cancellationToken);
            var letterViews = new List<MailLetterView>(letters.Count);

            foreach (var letter in letters)
                letterViews.Add(new MailLetterView(letter.Id, letter.Kind.ToString(), letter.CreatedAt, letter.IsRead,
                    letter.Kind == MailKind.ClanInvite ? await InviteAsync(letter, now, cancellationToken) : null));

            var announcements = await _mail.GetAnnouncementsAsync(now, cancellationToken);
            var read = await _mail.GetReadAnnouncementIdsAsync(request.PlayerId,
                announcements.Select(a => a.Id).ToList(), cancellationToken);

            var announcementViews = announcements
                .Select(a => new AnnouncementView(a.Id, a.Kind.ToString(), a.Title, a.Body, a.PublishedAt, read.Contains(a.Id)))
                .ToList();

            var unread = letterViews.Count(l => !l.IsRead) + announcementViews.Count(a => !a.IsRead);

            return new MailboxView(letterViews, announcementViews, unread);
        }

        /// <summary>
        /// Стан запрошення тепер, а не на момент листа: воно могло протермінуватись,
        /// а клан — розпастись. Кнопки — лише в того, що ще чекає.
        /// </summary>
        private async Task<ClanInviteLetterView?> InviteAsync(MailLetter letter, DateTime now, CancellationToken cancellationToken)
        {
            var invite = await _requests.GetByIdAsync(letter.ReferenceId, cancellationToken);

            if (invite is null)
                return null;

            var card = await _clans.GetCardAsync(invite.ClanId, cancellationToken);

            var state = card is null
                ? "Gone"
                : invite.Status == ClanRequestStatus.Pending && invite.ExpiresAt <= now
                    ? "Expired"
                    : invite.Status.ToString();

            return new ClanInviteLetterView(invite.Id, invite.ClanId, card?.Name ?? "—", card?.Tag ?? "—", state,
                invite.ExpiresAt, state == nameof(ClanRequestStatus.Pending));
        }
    }
}
