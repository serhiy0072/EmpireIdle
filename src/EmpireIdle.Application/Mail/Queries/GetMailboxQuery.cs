using EmpireIdle.Application.Clans.ReadModels;
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
        /// <summary>Скільки найновіших листів показує скринька: старші згорають за строком і так.</summary>
        public const int LetterLimit = 100;

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

            var letters = await _mail.GetLettersAsync(request.PlayerId, now, LetterLimit, cancellationToken);

            // Те, на що посилаються листи, — пакетом за типом листа, а не запитом на лист
            var invites = (await _requests.GetByIdsReadOnlyAsync(ReferencesOf(letters, MailKind.ClanInvite), cancellationToken))
                .ToDictionary(r => r.Id);
            var clans = invites.Count == 0
                ? new Dictionary<Guid, ClanCard>()
                : await _clans.GetCardsAsync(invites.Values.Select(r => r.ClanId).Distinct().ToList(), cancellationToken);
            var falls = (await _falls.GetByIdsAsync(ReferencesOf(letters, MailKind.CityFall), cancellationToken))
                .ToDictionary(f => f.Id);
            var structureFalls = (await _structureFalls.GetByIdsAsync(ReferencesOf(letters, MailKind.StructureFall), cancellationToken))
                .ToDictionary(f => f.Id);

            var letterViews = letters
                .Select(letter => new MailLetterView(letter.Id, letter.Kind.ToString(), letter.CreatedAt, letter.ExpiresAt,
                    letter.IsRead,
                    letter.Kind == MailKind.ClanInvite ? Invite(letter, invites, clans, now) : null,
                    letter.Kind == MailKind.CityFall ? Fall(letter, falls) : null,
                    letter.Kind == MailKind.StructureFall ? StructureFall(letter, structureFalls) : null,
                    letter.Rewards.Select(r => new MailRewardView(r.Type, r.Key, r.Amount)).ToList(),
                    letter.Sequence, letter.ClaimedAt, letter.CanClaimAt(now)))
                .ToList();

            var announcements = await _mail.GetAnnouncementsAsync(now, cancellationToken);
            var read = await _mail.GetReadAnnouncementIdsAsync(request.PlayerId,
                announcements.Select(a => a.Id).ToList(), cancellationToken);

            var announcementViews = announcements
                .Select(a => new AnnouncementView(a.Id, a.Kind.ToString(), a.Title, a.Body, a.PublishedAt, read.Contains(a.Id)))
                .ToList();

            // Листи рахує БД: список обрізаний до LetterLimit, а непрочитані можуть бути й старші
            var unread = await _mail.CountUnreadLettersAsync(request.PlayerId, now, cancellationToken)
                + announcementViews.Count(a => !a.IsRead);

            return new MailboxView(letterViews, announcementViews, unread);
        }

        private static List<Guid> ReferencesOf(IEnumerable<MailLetter> letters, MailKind kind)
            => letters.Where(l => l.Kind == kind && l.ReferenceId is not null).Select(l => l.ReferenceId!.Value).Distinct().ToList();

        private static CityFallLetterView? Fall(MailLetter letter, IReadOnlyDictionary<Guid, VillageFall> falls)
            => letter.ReferenceId is { } fallId && falls.TryGetValue(fallId, out var fall)
                ? new CityFallLetterView(fall.AttackerVillageName, fall.FromX, fall.FromY, fall.ToX, fall.ToY, fall.ShieldUntil)
                : null;

        private static StructureFallLetterView? StructureFall(MailLetter letter, IReadOnlyDictionary<Guid, StructureFall> falls)
            => letter.ReferenceId is { } fallId && falls.TryGetValue(fallId, out var fall)
                ? new StructureFallLetterView(fall.AttackerVillageName, fall.X, fall.Y, fall.OccurredAt)
                : null;

        /// <summary>
        /// Стан запрошення тепер, а не на момент листа: воно могло протермінуватись,
        /// а клан — розпастись. Кнопки — лише в того, що ще чекає.
        /// </summary>
        private static ClanInviteLetterView? Invite(MailLetter letter, IReadOnlyDictionary<Guid, ClanRequest> invites,
            IReadOnlyDictionary<Guid, ClanCard> clans, DateTime now)
        {
            if (letter.ReferenceId is not { } requestId || !invites.TryGetValue(requestId, out var invite))
                return null;

            var card = clans.GetValueOrDefault(invite.ClanId);

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
