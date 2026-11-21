using EmpireIdle.Domain.Events;

namespace EmpireIdle.Application.Quests.Tracking.Mappers
{
    /// <summary>Підношення клану: квест рахує кількість підношень, а не очки.</summary>
    public class ClanOfferingMadeMapper : QuestSignalMapper<ClanOfferingMade>
    {
        /// <inheritdoc/>
        protected override Task<QuestSignal?> Map(ClanOfferingMade e, CancellationToken cancellationToken)
            => Task.FromResult<QuestSignal?>(new QuestSignal(e.PlayerId, nameof(ClanOfferingMade), null, 1, null));
    }
}
