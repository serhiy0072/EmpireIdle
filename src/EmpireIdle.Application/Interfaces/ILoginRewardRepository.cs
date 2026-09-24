using EmpireIdle.Domain.Entities;

namespace EmpireIdle.Application.Interfaces
{
    /// <summary>Прогрес нагород за вхід (GDD §8.12).</summary>
    public interface ILoginRewardRepository
    {
        /// <summary>Прогрес гравця з трекінгом; null — ще жодного входу.</summary>
        Task<LoginRewardProgress?> GetByPlayerAsync(Guid playerId, CancellationToken cancellationToken = default);

        /// <summary>Перший вхід гравця. Гонку двох вкладок розв'язує унікальний індекс на PlayerId.</summary>
        Task AddAsync(LoginRewardProgress progress, CancellationToken cancellationToken = default);
    }
}
