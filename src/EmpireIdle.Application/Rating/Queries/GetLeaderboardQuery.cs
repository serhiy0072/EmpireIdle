using EmpireIdle.Application.Interfaces;
using EmpireIdle.Application.Rating.ReadModels;
using MediatR;

namespace EmpireIdle.Application.Rating.Queries
{ 
    /// <summary>
    /// Топ світу за рейтингом. Публічний у межах світу — на відміну від
    /// власної сили, чужий рейтинг гравець бачить: у цьому й сенс лідерборда.
    /// </summary>
    public record GetLeaderboardQuery(int Count = 100) : IRequest<List<LeaderboardEntry>>;

    public sealed class GetLeaderboardQueryHandler : IRequestHandler<GetLeaderboardQuery, List<LeaderboardEntry>>
    {
        private const int MaxCount = 200;

        private readonly IPlayerRatingRepository _ratingRepository;
        private readonly IPlayerRepository _playerRepository;
        private readonly IPlayerPowerRepository _powerRepository;

        public GetLeaderboardQueryHandler(
            IPlayerRatingRepository ratingRepository,
            IPlayerRepository playerRepository,
            IPlayerPowerRepository powerRepository)
        {
            _ratingRepository = ratingRepository;
            _playerRepository = playerRepository;
            _powerRepository = powerRepository;
        }

        public async Task<List<LeaderboardEntry>> Handle(GetLeaderboardQuery request, CancellationToken cancellationToken)
        {
            var count = Math.Clamp(request.Count, 1, MaxCount);

            var ratings = await _ratingRepository.GetTopAsync(count, cancellationToken);

            if (ratings.Count == 0)
                return [];

            var playerIds = ratings.Select(r => r.PlayerId).ToList();

            // Імена й сила одним запитом на всіх — інакше сто рядків топу
            // дали б двісті звернень до бази
            var names = await _playerRepository.GetNamesAsync(playerIds, cancellationToken);
            var powerByPlayer = await _powerRepository.GetTotalPowerAsync(playerIds, cancellationToken);

            return ratings
                .Select((rating, index) => new LeaderboardEntry(
                    index + 1,
                    rating.PlayerId,
                    names.GetValueOrDefault(rating.PlayerId, "—"),
                    rating.TotalRating,
                    powerByPlayer.GetValueOrDefault(rating.PlayerId)))
                .ToList();
        }
    }
}
