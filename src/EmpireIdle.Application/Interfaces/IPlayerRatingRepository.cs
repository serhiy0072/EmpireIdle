using EmpireIdle.Domain.Entities;

namespace EmpireIdle.Application.Interfaces
{
    /// <summary>Репозиторій серверного рейтингу.</summary>
    public interface IPlayerRatingRepository
    {
        /// <summary>
        /// Рейтинг гравця; null — гравець ще не потрапив у жоден прогін джоба.
        /// З трекінгом: підписники активності інкрементують лічильники.
        /// </summary>
        Task<PlayerRating?> GetByPlayerAsync(Guid playerId, CancellationToken cancellationToken = default);

        /// <summary>Топ світу за рейтингом.</summary>
        Task<List<PlayerRating>> GetTopAsync(int count, CancellationToken cancellationToken = default);

        /// <summary>Позиція гравця в топі, від 1.</summary>
        Task<int> GetRankAsync(Guid playerId, CancellationToken cancellationToken = default);

        /// <summary>Рейтинги всіх гравців світу. З трекінгом — джоб їх мутує.</summary>
        Task<List<PlayerRating>> GetAllAsync(CancellationToken cancellationToken = default);

        /// <summary>
        /// Створює рядок рейтингу. Викликається джобом для гравців, яких
        /// у таблиці ще немає — унікальний індекс на PlayerId не дасть другий.
        /// </summary>
        Task AddAsync(PlayerRating rating, CancellationToken cancellationToken = default);
    }
}
