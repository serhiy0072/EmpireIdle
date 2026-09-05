using EmpireIdle.Application.Common.Security;
using EmpireIdle.Application.Interfaces;
using EmpireIdle.Domain.Enums;
using EmpireIdle.Domain.Services;
using MediatR;

namespace EmpireIdle.Application.Clans.Queries
{
    /// <summary>Активний запит на кланову допомогу.</summary>
    public record ClanHelpItem(
        Guid RequestId,
        Guid PlayerId,
        string PlayerName,
        ClanHelpTarget TargetType,
        Guid TargetId,
        int HelpCount,
        int MaxHelpers,
        bool AlreadyHelped,
        bool IsMine,
        DateTime CreatedAt,
        DateTime ExpiresAt);

    /// <summary>Список допомоги клану — порожній, якщо гравець без клану.</summary>
    public record GetClanHelpQuery(Guid PlayerId) : IRequest<List<ClanHelpItem>>, IPlayerScopedRequest;

    /// <summary>Обробник GetClanHelpQuery.</summary>
    public sealed class GetClanHelpQueryHandler : IRequestHandler<GetClanHelpQuery, List<ClanHelpItem>>
    {
        private readonly IClanRepository _clanRepository;
        private readonly IClanHelpRepository _helpRepository;
        private readonly IPlayerRepository _playerRepository;
        private readonly GameCatalog _catalog;
        private readonly TimeProvider _timeProvider;

        public GetClanHelpQueryHandler(
            IClanRepository clanRepository,
            IClanHelpRepository helpRepository,
            IPlayerRepository playerRepository,
            GameCatalog catalog,
            TimeProvider timeProvider)
        {
            _clanRepository = clanRepository;
            _helpRepository = helpRepository;
            _playerRepository = playerRepository;
            _catalog = catalog;
            _timeProvider = timeProvider;
        }

        public async Task<List<ClanHelpItem>> Handle(GetClanHelpQuery request, CancellationToken cancellationToken)
        {
            // Потрібен лише id клану — склад тут не читається
            var clanId = await _clanRepository.GetClanIdByMemberAsync(request.PlayerId, cancellationToken);

            if (clanId is null)
                return [];

            var now = _timeProvider.GetUtcNow().UtcDateTime;

            var requests = await _helpRepository.GetActiveByClanAsync(clanId.Value, now, cancellationToken);

            if (requests.Count == 0)
                return [];

            var maxHelpers = _catalog.Config.Clan.MaxHelpers;

            var names = await _playerRepository.GetNamesAsync(
                requests.Select(r => r.PlayerId).Distinct().ToList(), cancellationToken);

            return requests
                .Select(r => new ClanHelpItem(
                    r.Id,
                    r.PlayerId,
                    names.GetValueOrDefault(r.PlayerId, "—"),
                    r.TargetType,
                    r.TargetId,
                    r.HelpCount,
                    maxHelpers,
                    r.Helpers.Any(h => h.HelperId == request.PlayerId),
                    r.PlayerId == request.PlayerId,
                    r.CreatedAt,
                    r.ExpiresAt))
                .ToList();
        }
    }
}
