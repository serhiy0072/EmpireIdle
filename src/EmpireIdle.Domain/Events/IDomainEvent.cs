namespace EmpireIdle.Domain.Events
{
    /// <summary>
    /// Маркерний інтерфейс для всіх доменних подій.
    /// Доменна подія — це щось що вже відбулось в системі.
    /// </summary>
    public interface IDomainEvent
    {
        /// <summary>Час коли подія відбулась.</summary>
        DateTime OccurredAt { get; }
    }
}
