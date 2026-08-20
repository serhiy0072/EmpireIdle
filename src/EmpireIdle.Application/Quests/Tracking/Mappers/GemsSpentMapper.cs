using EmpireIdle.Application.Quests.Tracking;
using EmpireIdle.Domain.Events;

/// <summary>Витрачені gems: приріст — сума.</summary>
public class GemsSpentMapper : QuestSignalMapper<GemsSpent>
{
    /// <inheritdoc/>
    protected override Task<QuestSignal?> Map(GemsSpent e, CancellationToken cancellationToken)
        => Task.FromResult<QuestSignal?>(new QuestSignal(e.PlayerId, nameof(GemsSpent), null, e.Amount.Value, null));
}
