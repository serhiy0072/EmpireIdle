using EmpireIdle.Domain.Enums;

namespace EmpireIdle.Domain.Events
{
    /// <summary>Подія: розвідники повернули звіт. Сповіщення — після коміту, з обробника outbox.</summary>
    public record ScoutReportFiled(Guid PlayerId, Guid ReportId, string TargetName, ScoutOutcome Outcome, DateTime OccurredAt)
        : IDomainEvent;
}
