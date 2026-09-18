using EmpireIdle.Domain.Entities;

namespace EmpireIdle.Application.Interfaces
{
    /// <summary>Прогрес pity й журнал роллів банерів.</summary>
    public interface IBannerRepository
    {
        /// <summary>Прогрес гравця в групі банерів. null — гравець ще не крутив.</summary>
        Task<BannerPityProgress?> GetPityAsync(Guid playerId, string pityGroup, CancellationToken cancellationToken = default);

        Task AddPityAsync(BannerPityProgress progress, CancellationToken cancellationToken = default);

        /// <summary>Додає рядок у журнал. Журнал лише поповнюється.</summary>
        Task AddRollAsync(BannerRollRecord record, CancellationToken cancellationToken = default);
    }
}
