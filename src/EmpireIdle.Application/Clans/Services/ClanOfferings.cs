using EmpireIdle.Application.Interfaces;
using EmpireIdle.Application.Rewards;
using EmpireIdle.Domain.Entities;
using EmpireIdle.Domain.Exceptions;
using EmpireIdle.Domain.Services;
using EmpireIdle.Domain.Services.Config;
using EmpireIdle.Domain.Technologies;

namespace EmpireIdle.Application.Clans.Services
{
    /// <summary>Що принесло одне підношення.</summary>
    /// <param name="DailyRewardsReached">Пороги денного внеску, які перетнуло це підношення; нагороди вже видано.</param>
    public record ClanOfferingResult(int Coins, long TotalCoins, long ClanPoints, IReadOnlyList<int> DailyRewardsReached);

    /// <summary>
    /// Спільне для підношень ресурсом і за gems: особиста книга й те, що дає саме підношення — очки в запас
    /// клану, внесок гравця й монети. Платіж лишається за командою: саме він у них різний.
    /// </summary>
    public sealed class ClanOfferings
    {
        private readonly IClanRepository _clans;
        private readonly IClanResearchRepository _research;
        private readonly IClanOfferingRepository _offerings;
        private readonly IServerContext _serverContext;
        private readonly ClanTechnologies _technologies;
        private readonly IRandomSource _random;
        private readonly RewardDispatcher _rewards;

        public ClanOfferings(IClanRepository clans, IClanResearchRepository research, IClanOfferingRepository offerings,
            IServerContext serverContext, ClanTechnologies technologies, IRandomSource random, RewardDispatcher rewards)
        {
            _clans = clans;
            _research = research;
            _offerings = offerings;
            _serverContext = serverContext;
            _technologies = technologies;
            _random = random;
            _rewards = rewards;
        }

        public async Task<Clan> ClanOfAsync(Guid playerId, CancellationToken cancellationToken)
            => await _clans.GetByMemberAsync(playerId, cancellationToken)
                ?? throw new InvalidStateException(RefusalReasons.ClanNotMember, "You are not in a clan.");

        /// <summary>Технології клану; перше підношення створює їх — гонку двох перших розв'язує індекс.</summary>
        public async Task<ClanResearch> ResearchOfAsync(Clan clan, DateTime utcNow, CancellationToken cancellationToken)
        {
            var research = await _research.GetByClanAsync(clan.Id, cancellationToken);

            if (research is null)
            {
                research = new ClanResearch(Guid.NewGuid(), clan.Id, _serverContext.ServerId, utcNow);
                await _research.AddAsync(research, cancellationToken);
            }

            return research;
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

        /// <summary>Зараховує оплачене підношення: очки в запас клану, внесок гравця в цей клан, монети.</summary>
        public async Task<ClanOfferingResult> RecordAsync(Clan clan, ClanResearch research, ClanOfferingLedger ledger, Guid playerId,
            int points, DateTime utcNow, CancellationToken cancellationToken)
        {
            var coinBonus = _technologies.Tree.Bonuses(research.Levels).Of(TechnologyStat.CoinBonus);
            var coins = _technologies.RollCoins(_random, coinBonus);

            research.AddPoints(points, utcNow);
            ledger.AddCoins(coins);

            // Пороги — за денною сумою гравця, а не внеском у клан: інакше перехід у інший клан дав би їх удруге
            var (before, after) = ledger.AddPoints(points, utcNow);
            var reached = _technologies.DailyRewardsCrossed(before, after).ToList();

            foreach (var threshold in reached)
                await _rewards.GrantAllAsync(playerId, threshold.Rewards,
                    $"clan-daily:{DateOnly.FromDateTime(utcNow):yyyy-MM-dd}:{threshold.Points}", utcNow, cancellationToken);

            var contribution = await _offerings.GetContributionAsync(clan.Id, playerId, cancellationToken);
            if (contribution is null)
            {
                contribution = new ClanContribution(Guid.NewGuid(), clan.Id, playerId, _serverContext.ServerId, utcNow);
                await _offerings.AddContributionAsync(contribution, cancellationToken);
            }

            contribution.Add(points, utcNow);
            clan.RecordActivity(playerId, utcNow);

            return new ClanOfferingResult(coins, ledger.Coins, research.UnallocatedPoints, reached.Select(r => r.Points).ToList());
        }
    }
}
