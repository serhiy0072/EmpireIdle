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

        /// <summary>Повідомити гравця про апгрейд будівлі.</summary>
        Task NotifyBuildingUpgradedAsync(Guid playerId, Guid buildingId, int newLevel, CancellationToken cancellationToken = default);
    }
}
