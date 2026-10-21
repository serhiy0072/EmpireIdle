using EmpireIdle.Application.Common.Security;
using EmpireIdle.Application.Interfaces;
using EmpireIdle.Application.Tower.ReadModels;
using MediatR;

namespace EmpireIdle.Application.Tower.Queries
{
    /// <summary>
    /// Рейтинг вежі на сервері (рішення 18.10.2026): найвищий пройдений поверх, за рівності вище той, хто
    /// дійшов раніше. Власний рядок — окремо, щоб гравець бачив своє місце й поза топом.
    /// </summary>
    public record GetTowerLeaderboardQuery(Guid PlayerId, int Count = 100) : IRequest<TowerLeaderboard>, IPlayerScopedRequest;

    public sealed class GetTowerLeaderboardQueryHandler : IRequestHandler<GetTowerLeaderboardQuery, TowerLeaderboard>
    {
        private const int MaxCount = 200;

        private readonly ITowerRepository _tower;
        private readonly IPlayerRepository _players;

        public GetTowerLeaderboardQueryHandler(ITowerRepository tower, IPlayerRepository players)
        {
            _tower = tower;
            _players = players;
        }

        public async Task<TowerLeaderboard> Handle(GetTowerLeaderboardQuery request, CancellationToken cancellationToken)
        {
            var top = await _tower.GetTopAsync(Math.Clamp(request.Count, 1, MaxCount), cancellationToken);
            var mine = await _tower.GetProgressReadOnlyAsync(request.PlayerId, cancellationToken);
            var myRank = mine is { HighestFloor: > 0 } ? await _tower.GetRankAsync(mine, cancellationToken) : (int?)null;

            // Імена одним запитом на всіх — інакше сто рядків топу дали б сто звернень до бази
            var names = await _players.GetNamesAsync(top.Select(p => p.PlayerId).Append(request.PlayerId).Distinct().ToList(), cancellationToken);

            var entries = top
                .Select((p, index) => new TowerLeaderboardEntry(index + 1, p.PlayerId, names.GetValueOrDefault(p.PlayerId, "—"), p.HighestFloor, p.ReachedAt))
                .ToList();

            var own = mine is null || myRank is null
                ? null
                : new TowerLeaderboardEntry(myRank.Value, mine.PlayerId, names.GetValueOrDefault(mine.PlayerId, "—"), mine.HighestFloor, mine.ReachedAt);

            return new TowerLeaderboard(entries, own);
        }
    }
}
