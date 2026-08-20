using EmpireIdle.Domain.Events;

namespace EmpireIdle.Application.Quests.Tracking.Mappers
{
    /// <summary>Апгрейд будівлі: приріст для лічильників, рівень для порогових цілей.</summary>
    public class BuildingUpgradeCompletedMapper : QuestSignalMapper<BuildingUpgradeCompleted>
    {
        /// <inheritdoc/>
        protected override Task<QuestSignal?> Map(BuildingUpgradeCompleted e, CancellationToken cancellationToken)
            => Task.FromResult<QuestSignal?>(new QuestSignal(e.PlayerId, nameof(BuildingUpgradeCompleted), e.BuildingType, 1, e.NewLevel.Value));
    }
}
