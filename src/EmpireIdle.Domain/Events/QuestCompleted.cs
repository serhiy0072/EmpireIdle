namespace EmpireIdle.Domain.Events
{
    /// <summary>Подія: квест виконано. Нагорода ще не видана — гравець забирає вручну.</summary>
    public record QuestCompleted(Guid PlayerId, string QuestKey, DateTime OccurredAt) : IDomainEvent;
}
