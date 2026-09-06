using EmpireIdle.Application.Interfaces;
using EmpireIdle.Domain.Services;
using MediatR;

namespace EmpireIdle.Application.Clans.Queries
{
    /// <summary>
    /// Клани, які треба перевірити на автопередачу. Не player-scoped:
    /// запит фоновий, світ приходить зі скоупу джоба.
    /// </summary>
    public record GetClansWithInactiveLeaderQuery : IRequest<IReadOnlyList<Guid>>;

    /// <summary>Обробник GetClansWithInactiveLeaderQuery.</summary>
    public sealed class GetClansWithInactiveLeaderQueryHandler
        : IRequestHandler<GetClansWithInactiveLeaderQuery, IReadOnlyList<Guid>>
    {
        private readonly IClanRepository _clanRepository;
        private readonly GameCatalog _catalog;
        private readonly TimeProvider _timeProvider;

        public GetClansWithInactiveLeaderQueryHandler(
            IClanRepository clanRepository,
            GameCatalog catalog,
            TimeProvider timeProvider)
        {
            _clanRepository = clanRepository;
            _catalog = catalog;
            _timeProvider = timeProvider;
        }

        public Task<IReadOnlyList<Guid>> Handle(GetClansWithInactiveLeaderQuery request,
            CancellationToken cancellationToken)
        {
            var now = _timeProvider.GetUtcNow().UtcDateTime;
            var inactiveSince = now.AddDays(-_catalog.Config.Clan.LeaderInactivityDays);

            return _clanRepository.GetIdsWithInactiveLeaderAsync(inactiveSince, cancellationToken);
        }
    }
}
