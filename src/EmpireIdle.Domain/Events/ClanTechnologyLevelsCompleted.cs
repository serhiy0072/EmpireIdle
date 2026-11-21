namespace EmpireIdle.Domain.Events
{
    /// <summary>Клан закрив рівні технології: сила кожного учасника зросла.</summary>
    public record ClanTechnologyLevelsCompleted(Guid ClanId, string Key, int Level, DateTime OccurredAt) : IDomainEvent;
}
