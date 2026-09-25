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
        private readonly IVillageFallRepository _falls;
        private readonly IStructureFallRepository _structureFalls;
        private readonly TimeProvider _timeProvider;

        public GetMailboxQueryHandler(IMailRepository mail, IClanRequestRepository requests, IClanRepository clans,
            IVillageFallRepository falls, IStructureFallRepository structureFalls, TimeProvider timeProvider)
        {
            _mail = mail;
            _requests = requests;
            _clans = clans;
            _falls = falls;
            _structureFalls = structureFalls;
            _timeProvider = timeProvider;
        }

        public async Task<MailboxView> Handle(GetMailboxQuery request, CancellationToken cancellationToken)
        {
            var now = _timeProvider.GetUtcNow().UtcDateTime;

            var letters = await _mail.GetLettersAsync(request.PlayerId, now, cancellationToken);
            var letterViews = new List<MailLetterView>(letters.Count);

            foreach (var letter in letters)
                letterViews.Add(new MailLetterView(letter.Id, letter.Kind.ToString(), letter.CreatedAt, letter.ExpiresAt,
                    letter.IsRead,
                    letter.Kind == MailKind.ClanInvite ? await InviteAsync(letter, now, cancellationToken) : null,
                    letter.Kind == MailKind.CityFall ? await FallAsync(letter, cancellationToken) : null,
                    letter.Kind == MailKind.StructureFall ? await StructureFallAsync(letter, cancellationToken) : null,
                    letter.Rewards.Select(r => new MailRewardView(r.Type, r.Key, r.Amount)).ToList(),
                    letter.Sequence, letter.ClaimedAt, letter.CanClaimAt(now)));

            var announcements = await _mail.GetAnnouncementsAsync(now, cancellationToken);
            var read = await _mail.GetReadAnnouncementIdsAsync(request.PlayerId,
                announcements.Select(a => a.Id).ToList(), cancellationToken);

            var announcementViews = announcements
                .Select(a => new AnnouncementView(a.Id, a.Kind.ToString(), a.Title, a.Body, a.PublishedAt, read.Contains(a.Id)))
                .ToList();

            var unread = letterViews.Count(l => !l.IsRead) + announcementViews.Count(a => !a.IsRead);

            return new MailboxView(letterViews, announcementViews, unread);
        }

        private async Task<CityFallLetterView?> FallAsync(MailLetter letter, CancellationToken cancellationToken)
            => letter.ReferenceId is { } fallId && await _falls.GetByIdAsync(fallId, cancellationToken) is { } fall
                ? new CityFallLetterView(fall.AttackerVillageName, fall.FromX, fall.FromY, fall.ToX, fall.ToY, fall.ShieldUntil)
                : null;

        private async Task<StructureFallLetterView?> StructureFallAsync(MailLetter letter, CancellationToken cancellationToken)
            => letter.ReferenceId is { } fallId && await _structureFalls.GetByIdAsync(fallId, cancellationToken) is { } fall
                ? new StructureFallLetterView(fall.AttackerVillageName, fall.X, fall.Y, fall.OccurredAt)
                : null;

        /// <summary>
        /// Стан запрошення тепер, а не на момент листа: воно могло протермінуватись,
        /// а клан — розпастись. Кнопки — лише в того, що ще чекає.
        /// </summary>
        private async Task<ClanInviteLetterView?> InviteAsync(MailLetter letter, DateTime now, CancellationToken cancellationToken)
        {
            if (letter.ReferenceId is not { } requestId)
                return null;

            var invite = await _requests.GetByIdAsync(requestId, cancellationToken);

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
