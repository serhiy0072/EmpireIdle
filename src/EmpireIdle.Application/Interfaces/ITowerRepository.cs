using EmpireIdle.Domain.Entities;

namespace EmpireIdle.Application.Interfaces
{
    /// <summary>Прогрес гравців у «Тисячній вежі».</summary>
    public interface ITowerRepository
    {
        /// <summary>Прогрес гравця для зміни або null, якщо він ще не пройшов жодного поверху.</summary>
        Task<TowerProgress?> GetProgressAsync(Guid playerId, CancellationToken cancellationToken = default);

        /// <summary>Прогрес гравця лише для читання.</summary>
        Task<TowerProgress?> GetProgressReadOnlyAsync(Guid playerId, CancellationToken cancellationToken = default);

        Task AddProgressAsync(TowerProgress progress, CancellationToken cancellationToken = default);

        /// <summary>Топ сервера: найвищий пройдений поверх, за рівності — хто дійшов раніше. Без жодного поверху в топ не потрапляють.</summary>
        Task<List<TowerProgress>> GetTopAsync(int count, CancellationToken cancellationToken = default);

        /// <summary>Місце прогресу в рейтингу сервера: скільки гравців стоять вище плюс один.</summary>
        Task<int> GetRankAsync(TowerProgress progress, CancellationToken cancellationToken = default);
    }
}
