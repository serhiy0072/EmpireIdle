using EmpireIdle.Application.Quests.Tracking;
using EmpireIdle.Domain.Events;

/// <summary>Бій: уточнення — результат, щоб рахувати перемоги й поразки окремо.</summary>
public class BattleFoughtMapper : QuestSignalMapper<BattleFought>
{
    /// <inheritdoc/>
    protected override Task<QuestSignal?> Map(BattleFought e, CancellationToken cancellationToken)
        => Task.FromResult<QuestSignal?>(new QuestSignal(e.PlayerId, nameof(BattleFought), e.Won ? "won" : "lost", 1, null));
}
