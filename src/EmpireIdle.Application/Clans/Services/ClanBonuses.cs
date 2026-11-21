using EmpireIdle.Application.Interfaces;
using EmpireIdle.Domain.Technologies;

namespace EmpireIdle.Application.Clans.Services
{
    /// <summary>
    /// Бонуси самого клану — для того, що належить клану, а не гравцю: споруд, стелі допомоги, кешбеку.
    /// Особисті рівні сюди не домішуються, тож споруду не підсилює той, хто просто в неї зайшов.
    /// </summary>
    public sealed class ClanBonuses
    {
        private readonly IClanResearchRepository _research;
        private readonly ClanTechnologies _technologies;

        public ClanBonuses(IClanResearchRepository research, ClanTechnologies technologies)
        {
            _research = research;
            _technologies = technologies;
        }

        public async Task<TechnologyBonuses> ForClanAsync(Guid clanId, CancellationToken cancellationToken)
            => await _research.GetLevelsByClanAsync(clanId, cancellationToken) is { } levels
                ? _technologies.Tree.Bonuses(levels)
                : TechnologyBonuses.None;

        /// <summary>Бонуси клану, у якому гравець зараз; поза кланом — порожні.</summary>
        public async Task<TechnologyBonuses> ForMemberAsync(Guid playerId, CancellationToken cancellationToken)
            => await _research.GetLevelsByMemberAsync(playerId, cancellationToken) is { } levels
                ? _technologies.Tree.Bonuses(levels)
                : TechnologyBonuses.None;
    }
}
