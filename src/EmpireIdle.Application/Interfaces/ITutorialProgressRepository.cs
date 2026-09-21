using EmpireIdle.Domain.Entities;

namespace EmpireIdle.Application.Interfaces
{
    /// <summary>Репозиторій прогресу навчання: один рядок на гравця, створюється на першому кроці.</summary>
    public interface ITutorialProgressRepository
    {
        /// <summary>Прогрес гравця або null, якщо він ще не бачив жодної підказки.</summary>
        Task<TutorialProgress?> GetByPlayerAsync(Guid playerId, CancellationToken cancellationToken = default);

        /// <summary>Те саме без трекінгу — для читання.</summary>
        Task<TutorialProgress?> GetByPlayerReadOnlyAsync(Guid playerId, CancellationToken cancellationToken = default);

        Task AddAsync(TutorialProgress progress, CancellationToken cancellationToken = default);
    }
}
