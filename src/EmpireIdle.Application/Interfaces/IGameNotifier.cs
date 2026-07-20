namespace EmpireIdle.Application.Interfaces
{
    /// <summary>
    /// Абстракція для real-time сповіщень гравцям.
    /// Application не знає про SignalR — реалізація в API/Infrastructure.
    /// </summary>
    public interface IGameNotifier
    {
        /// <summary>Сповістити гравця про зібрані з будівлі ресурси та новий баланс.</summary>
        Task NotifyBuildingCollectedAsync(Guid playerId, Guid buildingId, string resourceType, int collected, int newVillageAmount, CancellationToken cancellationToken = default);
        
        /// <summary>Повідомити гравця про старт апгрейду (для таймера на фронті).</summary>
        Task NotifyUpgradeStartedAsync(Guid playerId, Guid buildingId, DateTime completesAt, CancellationToken cancellationToken = default);

        /// <summary>Повідомити гравця про завершення апгрейду (новий рівень).</summary>
        Task NotifyUpgradeCompletedAsync(Guid playerId, Guid buildingId, int newLevel, CancellationToken cancellationToken = default);
    }
}
