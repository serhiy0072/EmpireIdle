using EmpireIdle.Domain.Entities;

namespace EmpireIdle.Application.Interfaces
{
    public interface IPlayerRatingRepository
    {
        Task<PlayerRating?> GetByPlayerAsync(Guid playerId, CancellationToken cancellationToken = default);

        /// <summary>Топ світу за рейтингом.</summary>
        Task<List<PlayerRating>> GetTopAsync(int count, CancellationToken cancellationToken = default);

        /// <summary>Позиція гравця в топі, від 1.</summary>
        Task<int> GetRankAsync(Guid playerId, CancellationToken cancellationToken = default);

        Task AddAsync(PlayerRating rating, CancellationToken cancellationToken = default);
    }
}
