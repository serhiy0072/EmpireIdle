using EmpireIdle.Application.Interfaces;
using EmpireIdle.Domain.Entities;
using EmpireIdle.Domain.Exceptions;
using EmpireIdle.Domain.Services;
using EmpireIdle.Domain.Services.Config;
using EmpireIdle.Domain.Technologies;

namespace EmpireIdle.Application.Clans.Services
{
    /// <summary>Що принесло одне підношення.</summary>
    public record ClanOfferingResult(int Coins, long TotalCoins, IReadOnlyList<string> LevelsCompleted);

    /// <summary>
    /// Спільне для підношень ресурсом і за gems: ціль клану, особиста книга й те, що дає саме підношення —
    /// очки клану, внесок гравця й монети. Платіж лишається за командою: саме він у них різний.
    /// </summary>
    public sealed class ClanOfferings
    {
        private readonly IClanRepository _clans;
        private readonly IClanResearchRepository _research;
        private readonly IClanOfferingRepository _offerings;
        private readonly IServerContext _serverContext;
        private readonly ClanTechnologies _technologies;
        private readonly IRandomSource _random;

        public ClanOfferings(IClanRepository clans, IClanResearchRepository research, IClanOfferingRepository offerings,
            IServerContext serverContext, ClanTechnologies technologies, IRandomSource random)
        {
            _clans = clans;
            _research = research;
            _offerings = offerings;
            _serverContext = serverContext;
            _technologies = technologies;
            _random = random;
        }

        public async Task<Clan> ClanOfAsync(Guid playerId, CancellationToken cancellationToken)
            => await _clans.GetByMemberAsync(playerId, cancellationToken)
                ?? throw new InvalidStateException(RefusalReasons.ClanNotMember, "You are not in a clan.");

        /// <summary>Технології клану з обраною ціллю; цілі немає — підносити нема на що.</summary>
        public async Task<(ClanResearch Research, TechnologyNodeConfig Node)> TargetOfAsync(Clan clan, CancellationToken cancellationToken)
        {
            var research = await _research.GetByClanAsync(clan.Id, cancellationToken);

            if (research?.TargetKey is { } target
                && TechnologyKey.Parse(target) is { } key
                && _technologies.Tree.Node(key.Node) is { } node)
                return (research, node);

            throw new RequirementNotMetException(RefusalReasons.ClanTechNotSelected, $"Clan {clan.Id} has no technology selected.");
        }

        public async Task<ClanOfferingLedger> LedgerOfAsync(Guid playerId, DateTime utcNow, CancellationToken cancellationToken)
        {
            var ledger = await _offerings.GetLedgerAsync(playerId, cancellationToken);

            if (ledger is null)
            {
                ledger = new ClanOfferingLedger(Guid.NewGuid(), playerId, _serverContext.ServerId, utcNow);
                await _offerings.AddLedgerAsync(ledger, cancellationToken);
            }

            return ledger;
        }

        /// <summary>Зараховує оплачене підношення: очки цілі, внесок гравця в цей клан, монети.</summary>
        public async Task<ClanOfferingResult> RecordAsync(Clan clan, ClanResearch research, ClanOfferingLedger ledger, Guid playerId,
            int points, DateTime utcNow, CancellationToken cancellationToken)
        {
            // Бонус монет — до нарахування очок: рівень, який закриє це підношення, діє вже з наступного
            var coinBonus = _technologies.Tree.Bonuses(research.Levels).Of(TechnologyStat.CoinBonus);
            var coins = _technologies.RollCoins(_random, coinBonus);

            var completed = research.AddPoints(points, _technologies, utcNow);

            ledger.AddCoins(coins);
            ledger.AddPoints(points, utcNow);

            var contribution = await _offerings.GetContributionAsync(clan.Id, playerId, cancellationToken);
            if (contribution is null)
            {
                contribution = new ClanContribution(Guid.NewGuid(), clan.Id, playerId, _serverContext.ServerId, utcNow);
                await _offerings.AddContributionAsync(contribution, cancellationToken);
            }

            contribution.Add(points, utcNow);
            clan.RecordActivity(playerId, utcNow);

            return new ClanOfferingResult(coins, ledger.Coins, completed.Select(c => c.Key.ToString()).ToList());
        }
    }
}
