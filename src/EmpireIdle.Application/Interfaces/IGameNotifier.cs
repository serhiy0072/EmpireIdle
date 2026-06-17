namespace EmpireIdle.Application.Interfaces
{
    /// <summary>
    /// Абстракція для real-time сповіщень гравцям.
    /// Application не знає про SignalR — реалізація в API/Infrastructure.
    /// </summary>
    public interface IGameNotifier
    {
        /// <summary>Повідомити гравця про оновлення ресурсів у його селі.</summary>
        Task NotifyResourcesUpdatedAsync(Guid playerId, IReadOnlyDictionary<string, int> resources, CancellationToken cancellationToken = default);

        /// <summary>Повідомити гравця про апгрейд будівлі.</summary>
        Task NotifyBuildingUpgradedAsync(Guid playerId, Guid buildingId, int newLevel, CancellationToken cancellationToken = default);
    }
}
