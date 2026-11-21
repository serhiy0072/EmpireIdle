using EmpireIdle.Application.Interfaces;
using EmpireIdle.Domain.Technologies;

namespace EmpireIdle.Application.Research.Services
{
    /// <summary>
    /// Бонуси технологій гравця — те, що читають бій, марш, таймери й виробіток: особисті плюс кланові.
    /// Рахуються «наживо» з вивчених рівнів, нічого не кешують: змінився рівень чи клан — змінився й бонус,
    /// а вихід із клану забирає його бонуси того ж моменту (рішення 03.11).
    /// </summary>
    public class PlayerTechnologies
    {
        private readonly IPlayerResearchRepository _research;
        private readonly PersonalTechnologies _personal;
        private readonly IClanResearchRepository _clanResearch;
        private readonly ClanTechnologies _clan;

        public PlayerTechnologies(IPlayerResearchRepository research, PersonalTechnologies personal,
            IClanResearchRepository clanResearch, ClanTechnologies clan)
        {
            _research = research;
            _personal = personal;
            _clanResearch = clanResearch;
            _clan = clan;
        }

        /// <summary>Усі бонуси гравця; без досліджень і поза кланом — порожні.</summary>
        public async Task<TechnologyBonuses> ForPlayerAsync(Guid playerId, CancellationToken cancellationToken)
        {
            var research = await _research.GetByPlayerReadOnlyAsync(playerId, cancellationToken);
            var clanLevels = await _clanResearch.GetLevelsByMemberAsync(playerId, cancellationToken);

            var personal = research is null ? TechnologyBonuses.None : _personal.Tree.Bonuses(research.Levels);

            return clanLevels is null ? personal : personal.Plus(_clan.Tree.Bonuses(clanLevels));
        }
    }
}
