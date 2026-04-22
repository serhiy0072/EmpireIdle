using EmpireIdle.Domain.ValueObjects;

namespace EmpireIdle.Domain.Events
{
    /// <summary>
    /// Подія що виникає коли гравець витрачає gems на внутрішньоігрові покупки.
    /// </summary>
    public record GemsSpent(Guid PlayerId, GemAmount Amount, GemAmount NewBalance, string Description) : IDomainEvent
    {
        public DateTime OccurredAt { get; } = DateTime.UtcNow;
    }
}
