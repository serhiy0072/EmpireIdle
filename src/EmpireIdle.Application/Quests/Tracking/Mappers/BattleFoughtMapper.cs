using EmpireIdle.Domain.Events;

namespace EmpireIdle.Application.Quests.Tracking.Mappers
{
    /// <summary>Бій: уточнення — результат, щоб рахувати перемоги й поразки окремо.</summary>
    public class BattleFoughtMapper : QuestSignalMapper<BattleFought>
    {
        /// <inheritdoc/>
        protected override Task<QuestSignal?> Map(BattleFought e, CancellationToken cancellationToken)
            => Task.FromResult<QuestSignal?>(new QuestSignal(e.PlayerId, nameof(BattleFought), e.Won ? "won" : "lost", 1, null));
    }
}
