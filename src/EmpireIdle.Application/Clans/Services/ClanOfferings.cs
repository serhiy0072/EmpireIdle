using EmpireIdle.Application.Interfaces;
using EmpireIdle.Application.Research.Services;
using EmpireIdle.Application.Rewards;
using EmpireIdle.Domain.Entities;
using EmpireIdle.Domain.Enums;
using EmpireIdle.Domain.Exceptions;
using EmpireIdle.Domain.Services;
using EmpireIdle.Domain.Services.Config;
using EmpireIdle.Domain.Technologies;

namespace EmpireIdle.Application.Clans.Services
{
    /// <summary>Що принесло одне підношення.</summary>
    /// <param name="Level">Рівень вузла після підношення.</param>
    /// <param name="Progress">Вкладено в наступний рівень вузла після підношення.</param>
    /// <param name="LevelsCompleted">Рівні, які закрило саме це підношення.</param>
    /// <param name="Marked">Вузол мав мітку керівництва: монети й очки денних порогів уже з надбавкою.</param>
    /// <param name="DailyRewardsReached">Пороги денного внеску, які перетнуло це підношення; нагороди вже видано.</param>
    public record ClanOfferingResult(int Coins, long TotalCoins, int Level, int Progress, IReadOnlyList<int> LevelsCompleted,
        bool Marked, IReadOnlyList<int> DailyRewardsReached);

    /// <summary>
    /// Спільне для підношень ресурсом і за gems: доступність вузла, особиста книга й те, що дає саме
    /// підношення — очки у вузол, внесок гравця й монети. Платіж лишається за командою: саме він у них різний.
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
        private readonly ResearchSettlement _settlement;

        public ClanOfferings(IClanRepository clans, IClanResearchRepository research, IClanOfferingRepository offerings,
            IServerContext serverContext, ClanTechnologies technologies, IRandomSource random, RewardDispatcher rewards,
            ResearchSettlement settlement)
        {
            _clans = clans;
            _research = research;
            _offerings = offerings;
            _serverContext = serverContext;
            _technologies = technologies;
            _random = random;
            _rewards = rewards;
            _settlement = settlement;
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

        /// <summary>
        /// Вузол, у який можна підносити зараз. Перевіряється до платежу: підношення в закритий чи вивчений
        /// вузол нічого б не дало, а гравець заплатив би. Ратуші й рівня світу клан не має (рішення 12.11): тір
        /// відкриває лише попередній шар.
        /// </summary>
        public TechnologyNodeConfig AvailableNode(ClanResearch research, TechnologyKey key)
        {
            var node = _technologies.Tree.Node(key.Node)
                ?? throw new EntityNotFoundException("Technology", key.Node);

            switch (_technologies.Tree.Availability(research.Levels, key.Tier, node.Key, int.MaxValue, int.MaxValue))
            {
                case TechnologyAvailability.Maxed:
                    throw new RequirementNotMetException(RefusalReasons.ResearchMaxed, $"{node.Key} is maxed on tier {key.Tier}.");

                case TechnologyAvailability.PreviousTierIncomplete:
                    throw new RequirementNotMetException(RefusalReasons.ResearchPreviousTier,
                        $"Tier {key.Tier - 1} of {node.Tab} is not finished.", key.Tier - 1);

                case TechnologyAvailability.PrerequisitesMissing:
                    var missing = node.Requires.Where(r => TechnologyTree.LevelOf(research.Levels, key.Tier, r) < 1);
                    throw new RequirementNotMetException(RefusalReasons.ResearchPrerequisites,
                        $"{node.Key} needs a level of its prerequisites first.", string.Join(",", missing));

                case TechnologyAvailability.UnknownNode:
                case TechnologyAvailability.TierGated:
                    throw new EntityNotFoundException("Technology", key.ToString());
            }

            return node;
        }

        /// <summary>
        /// Зараховує оплачене підношення: очки у вузол, внесок гравця в цей клан, монети. Внесок у вузол із міткою
        /// керівництва дає надбавку до монет і очок денних порогів, але не до поступу й рейтингу. Новий рівень виробітку
        /// діє лише відтепер, тож кожне село клану спершу фіксує вироблене за старою ставкою.
        /// </summary>
        public async Task<ClanOfferingResult> RecordAsync(Clan clan, ClanResearch research, ClanOfferingLedger ledger, Guid playerId,
            TechnologyKey key, TechnologyNodeConfig node, int points, DateTime utcNow, CancellationToken cancellationToken)
        {
            var coinBonus = _technologies.Tree.Bonuses(research.Levels).Of(TechnologyStat.CoinBonus);
            var coins = _technologies.RollCoins(_random, coinBonus);

            // Мітку читаємо до вкладення: підношення, що добиває вузол до кінця, знімає її, але бонус ще отримує
            var marked = research.IsMarked(key);
            var dailyPoints = points;

            if (marked)
            {
                var multiplier = 1 + Math.Max(_technologies.Offering.MarkedBonus, 0);
                coins = (int)Math.Round(coins * multiplier, MidpointRounding.AwayFromZero);
                dailyPoints = (int)Math.Round(points * multiplier, MidpointRounding.AwayFromZero);
            }

            // Сотні сіл в одній транзакції, але рівень виробітку закривається рідко. Ставку ще віддає БД,
            // тому фіксуємо до вкладення
            var completesProduction = node.Effects.Any(e => e.Stat == TechnologyStat.Production)
                && research.ProgressOf(key) + points >= _technologies.Points(key.Tier, node, research.LevelOf(key) + 1);

            if (completesProduction)
                foreach (var member in clan.Members)
                    await _settlement.SettleVillageAsync(member.PlayerId, utcNow, cancellationToken);

            var completed = research.Invest(key, points, _technologies, utcNow);
            ledger.AddCoins(coins);

            // Пороги — за денною сумою гравця, а не внеском у клан: інакше перехід у інший клан дав би їх удруге
            var (before, after) = ledger.AddPoints(dailyPoints, utcNow);
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

            return new ClanOfferingResult(coins, ledger.Coins, research.LevelOf(key), research.ProgressOf(key),
                completed.Select(c => c.Level).ToList(), marked, reached.Select(r => r.Points).ToList());
        }
    }
}
