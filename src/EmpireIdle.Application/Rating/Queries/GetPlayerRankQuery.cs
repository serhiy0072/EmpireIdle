using EmpireIdle.Application.Common.Security;
using EmpireIdle.Application.Interfaces;
using EmpireIdle.Application.Rating.ReadModels;
using MediatR;

namespace EmpireIdle.Application.Rating.Queries
{    public record GetPlayerRankQuery(Guid PlayerId) : IRequest<PlayerRankView>, IPlayerScopedRequest;

    public sealed class GetPlayerRankQueryHandler : IRequestHandler<GetPlayerRankQuery, PlayerRankView>
    {
        private readonly IPlayerRatingRepository _ratingRepository;
        private readonly TimeProvider _timeProvider;

        public GetPlayerRankQueryHandler(IPlayerRatingRepository ratingRepository, TimeProvider timeProvider)
        {
            _ratingRepository = ratingRepository;
            _timeProvider = timeProvider;
        }

        public async Task<PlayerRankView> Handle(GetPlayerRankQuery request, CancellationToken cancellationToken)
        {
            var rating = await _ratingRepository.GetByPlayerAsync(request.PlayerId, cancellationToken);

            // Рядка ще немає — гравець не потрапив у жоден прогін джоба.
            // Нулі, а не 404: рейтинг нуль коректний, гравець існує
            if (rating is null)
                return new PlayerRankView(0, 0, 0, 0, 0, 0, 0, 0, _timeProvider.GetUtcNow().UtcDateTime);

            var rank = await _ratingRepository.GetRankAsync(request.PlayerId, cancellationToken);

            return new PlayerRankView(
                rank, rating.TotalRating,
                rating.PowerScore, rating.DevelopmentScore, rating.ActivityScore,
                rating.MonstersDefeated, rating.BattlesWon, rating.QuestsCompleted,
                rating.UpdatedAt);
        }
    }
}
