using EmpireIdle.Application.Quests.Tracking;

using EmpireIdle.Domain.Events;

/// <summary>Збір ресурсу: уточнення — тип ресурсу, приріст — зібрана кількість.</summary>
public class BuildingCollectedMapper : QuestSignalMapper<BuildingCollected>
{
    /// <inheritdoc/>
    protected override Task<QuestSignal?> Map(BuildingCollected e, CancellationToken cancellationToken)
        => Task.FromResult<QuestSignal?>(new QuestSignal(e.PlayerId, nameof(BuildingCollected), e.ResourceType, e.Amount, null));
}
