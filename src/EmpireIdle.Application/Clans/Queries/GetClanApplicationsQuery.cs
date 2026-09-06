using EmpireIdle.Application.Clans.ReadModels;
using EmpireIdle.Application.Common.Security;
using EmpireIdle.Application.Interfaces;
using EmpireIdle.Domain.Enums;
using MediatR;

namespace EmpireIdle.Application.Clans.Queries
{
    /// <summary>
    /// Черга заявок клану. Порожній список, якщо гравець без клану;
    /// відмова, якщо він у клані, але без права рекрутингу.
    /// </summary>
    public record GetClanApplicationsQuery(Guid PlayerId)
        : IRequest<List<ClanApplicationItem>>, IPlayerScopedRequest;

    /// <summary>Обробник GetClanApplicationsQuery.</summary>
    public sealed class GetClanApplicationsQueryHandler
        : IRequestHandler<GetClanApplicationsQuery, List<ClanApplicationItem>>
    {
        private readonly IClanRepository _clanRepository;
        private readonly IClanRequestRepository _requestRepository;
        private readonly IPlayerRepository _playerRepository;
        private readonly IPlayerPowerRepository _powerRepository;
        private readonly TimeProvider _timeProvider;

        public GetClanApplicationsQueryHandler(
            IClanRepository clanRepository,
            IClanRequestRepository requestRepository,
            IPlayerRepository playerRepository,
            IPlayerPowerRepository powerRepository,
            TimeProvider timeProvider)
        {
            _clanRepository = clanRepository;
            _requestRepository = requestRepository;
            _playerRepository = playerRepository;
            _powerRepository = powerRepository;
            _timeProvider = timeProvider;
        }

        public async Task<List<ClanApplicationItem>> Handle(GetClanApplicationsQuery request,
            CancellationToken cancellationToken)
        {
            var clan = await _clanRepository.GetByMemberAsync(request.PlayerId, cancellationToken);

            if (clan is null)
                return [];

            clan.EnsureCan(request.PlayerId, ClanPermission.Recruit);

            var now = _timeProvider.GetUtcNow().UtcDateTime;

            var applications = await _requestRepository.GetPendingByClanAsync(
                clan.Id, ClanRequestKind.Application, now, cancellationToken);

            if (applications.Count == 0)
                return [];

            var playerIds = applications.Select(a => a.PlayerId).Distinct().ToList();

            var names = await _playerRepository.GetNamesAsync(playerIds, cancellationToken);
            var powerByPlayer = await _powerRepository.GetTotalPowerAsync(playerIds, cancellationToken);

            return applications
                .Select(a => new ClanApplicationItem(
                    a.Id,
                    a.PlayerId,
                    names.GetValueOrDefault(a.PlayerId, "—"),
                    powerByPlayer.GetValueOrDefault(a.PlayerId),
                    a.CreatedAt,
                    a.ExpiresAt))
                .OrderByDescending(a => a.Power)
                .ToList();
        }
    }
}
