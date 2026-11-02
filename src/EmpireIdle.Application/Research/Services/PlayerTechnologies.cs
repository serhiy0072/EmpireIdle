using EmpireIdle.Application.Interfaces;
using EmpireIdle.Domain.Technologies;

namespace EmpireIdle.Application.Research.Services
{
    /// <summary>
    /// Бонуси технологій гравця — те, що читають бій, марш, таймери й виробіток.
    /// Рахуються «наживо» з вивчених рівнів, нічого не кешують: змінився рівень — змінився й бонус.
    /// </summary>
    public class PlayerTechnologies
    {
        private readonly IPlayerResearchRepository _research;
        private readonly PersonalTechnologies _personal;

        public PlayerTechnologies(IPlayerResearchRepository research, PersonalTechnologies personal)
        {
            _research = research;
            _personal = personal;
        }

        /// <summary>Бонуси особистих технологій гравця; без жодного дослідження — порожні.</summary>
        public async Task<TechnologyBonuses> ForPlayerAsync(Guid playerId, CancellationToken cancellationToken)
        {
            var research = await _research.GetByPlayerReadOnlyAsync(playerId, cancellationToken);

            return research is null ? TechnologyBonuses.None : _personal.Tree.Bonuses(research.Levels);
        }
    }
}
