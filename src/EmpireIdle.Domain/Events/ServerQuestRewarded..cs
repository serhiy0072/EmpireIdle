namespace EmpireIdle.Domain.Events
{
    /// <summary>
    /// Подія: гравцю видано нагороду за серверний квест.
    /// Несе ранг і внесок — без них gems з'являються нізвідки,
    /// і гравець не бачить зв'язку зі своєю участю.
    /// </summary>
    public record ServerQuestRewarded(
        Guid PlayerId,
        string QuestKey,
        int Rank,
        long Contribution,
        DateTime OccurredAt) : IDomainEvent;
}
