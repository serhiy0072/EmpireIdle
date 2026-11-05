using EmpireIdle.Domain.Events;
using EmpireIdle.Domain.Technologies;

namespace EmpireIdle.Application.Quests.Tracking.Mappers
{
    /// <summary>Вивчений рівень технології. Уточнення — вкладка дерева: квест може просити саме бойові.</summary>
    public class ResearchCompletedMapper : QuestSignalMapper<ResearchCompleted>
    {
        private readonly PersonalTechnologies _technologies;

        public ResearchCompletedMapper(PersonalTechnologies technologies) => _technologies = technologies;

        /// <inheritdoc/>
        protected override Task<QuestSignal?> Map(ResearchCompleted e, CancellationToken cancellationToken)
        {
            var tab = TechnologyKey.Parse(e.Key) is { } key ? _technologies.Tree.Node(key.Node)?.Tab : null;

            return Task.FromResult<QuestSignal?>(new QuestSignal(e.PlayerId, nameof(ResearchCompleted), tab, 1, null));
        }
    }
}
