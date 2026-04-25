namespace EmpireIdle.Application.Interfaces
{
    /// <summary>
    /// Сервіс для нарахування ресурсів у всіх селах.
    /// Викликається Hangfire recurring job кожну хвилину.
    /// </summary>
    public interface IResourceTickService
    {
        /// <summary>Обробити тік ресурсів для всіх сел.</summary>
        Task TickAllVillagesAsync(CancellationToken cancellationToken = default);
    }
}
