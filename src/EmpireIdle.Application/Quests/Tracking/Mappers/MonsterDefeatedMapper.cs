using EmpireIdle.Application.Quests.Tracking;

using EmpireIdle.Domain.Events;

/// <summary>Убитий монстр: уточнення — тип монстра.</summary>
public class MonsterDefeatedMapper : QuestSignalMapper<MonsterDefeated>
{
    /// <inheritdoc/>
    protected override Task<QuestSignal?> Map(MonsterDefeated e, CancellationToken cancellationToken)
        => Task.FromResult<QuestSignal?>(new QuestSignal(e.PlayerId, nameof(MonsterDefeated), e.MonsterType, 1, null));
}
