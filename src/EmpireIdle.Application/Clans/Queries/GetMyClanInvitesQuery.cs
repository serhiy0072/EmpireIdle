using EmpireIdle.Application.Clans.ReadModels;
using EmpireIdle.Application.Common.Security;
using EmpireIdle.Application.Interfaces;
using EmpireIdle.Domain.Enums;
using EmpireIdle.Domain.Services;
using MediatR;

namespace EmpireIdle.Application.Clans.Queries
{
    /// <summary>Запрошення, що чекають рішення гравця.</summary>
    public record GetMyClanInvitesQuery(Guid PlayerId)
        : IRequest<List<ClanInviteItem>>, IPlayerScopedRequest;

    /// <summary>Обробник GetMyClanInvitesQuery.</summary>
    public sealed class GetMyClanInvitesQueryHandler : IRequestHandler<GetMyClanInvitesQuery, List<ClanInviteItem>>
    {
        private readonly IClanRequestRepository _requestRepository;
        private readonly IClanRepository _clanRepository;
        private readonly GameCatalog _catalog;
        private readonly TimeProvider _timeProvider;

        public GetMyClanInvitesQueryHandler(
            IClanRequestRepository requestRepository,
            IClanRepository clanRepository,
            GameCatalog catalog,
            TimeProvider timeProvider)
        {
            _requestRepository = requestRepository;
            _clanRepository = clanRepository;
            _catalog = catalog;
            _timeProvider = timeProvider;
        }

        public async Task<List<ClanInviteItem>> Handle(GetMyClanInvitesQuery request,
            CancellationToken cancellationToken)
        {
            var now = _timeProvider.GetUtcNow().UtcDateTime;

            var invites = await _requestRepository.GetPendingByPlayerAsync(
                request.PlayerId, ClanRequestKind.Invite, now, cancellationToken);

            if (invites.Count == 0)
                return [];

            var clanIds = invites.Select(i => i.ClanId).Distinct().ToList();

            var cards = await _clanRepository.GetCardsAsync(clanIds, cancellationToken);
            var capacity = _catalog.Config.Clan.Capacity;

            return invites
                // Клан міг розпастись, поки запрошення чекало
                .Where(i => cards.ContainsKey(i.ClanId))
                .Select(i =>
                {
                    var card = cards[i.ClanId];

                    return new ClanInviteItem(
                        i.Id, card.Id, card.Name, card.Tag, card.Description,
                        card.MemberCount, capacity, i.CreatedAt, i.ExpiresAt);
                })
                .OrderBy(i => i.ExpiresAt)
                .ToList();
        }
    }
}
