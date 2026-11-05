using EmpireIdle.Application.Clans.ReadModels;
using EmpireIdle.Application.Common.Security;
using EmpireIdle.Application.Interfaces;
using EmpireIdle.Application.Research.ReadModels;
using EmpireIdle.Application.Research.Services;
using EmpireIdle.Domain.Entities;
using EmpireIdle.Domain.Services;
using EmpireIdle.Domain.Services.Config;
using EmpireIdle.Domain.Technologies;
using MediatR;

namespace EmpireIdle.Application.Clans.Queries
{
    /// <summary>Кланові технології для екрана; null — гравець без клану, це нормальний стан, а не помилка.</summary>
    public record GetClanTechnologiesQuery(Guid PlayerId) : IRequest<ClanTechnologiesView?>, IPlayerScopedRequest;

    public sealed class GetClanTechnologiesQueryHandler : IRequestHandler<GetClanTechnologiesQuery, ClanTechnologiesView?>
    {
        private readonly IClanRepository _clans;
        private readonly IClanResearchRepository _research;
        private readonly IClanOfferingRepository _offerings;
        private readonly IPlayerRepository _players;
        private readonly IVillageRepository _villages;
        private readonly IServerRepository _servers;
        private readonly IServerContext _serverContext;
        private readonly ClanTechnologies _technologies;
        private readonly VillageStatus _status;
        private readonly GameCatalog _catalog;
        private readonly TimeProvider _timeProvider;

        public GetClanTechnologiesQueryHandler(IClanRepository clans, IClanResearchRepository research, IClanOfferingRepository offerings,
            IPlayerRepository players, IVillageRepository villages, IServerRepository servers, IServerContext serverContext,
            ClanTechnologies technologies, VillageStatus status, GameCatalog catalog, TimeProvider timeProvider)
        {
            _clans = clans;
            _research = research;
            _offerings = offerings;
            _players = players;
            _villages = villages;
            _servers = servers;
            _serverContext = serverContext;
            _technologies = technologies;
            _status = status;
            _catalog = catalog;
            _timeProvider = timeProvider;
        }

        public async Task<ClanTechnologiesView?> Handle(GetClanTechnologiesQuery request, CancellationToken cancellationToken)
        {
            if (await _clans.GetClanIdByMemberAsync(request.PlayerId, cancellationToken) is not { } clanId)
                return null;

            var now = _timeProvider.GetUtcNow().UtcDateTime;
            var serverId = _serverContext.ServerId;

            var village = await _villages.GetByPlayerIdReadOnlyAsync(request.PlayerId, cancellationToken)
                ?? throw new InvalidOperationException($"Village not found for player {request.PlayerId}.");
            var worldLevel = await _servers.GetLevelAsync(serverId, cancellationToken);

            // Запит нічого не створює: рядків ще немає — показуємо порожній стан із тими самими правилами
            var research = await _research.GetByClanReadOnlyAsync(clanId, cancellationToken)
                ?? new ClanResearch(Guid.Empty, clanId, serverId, now);
            var ledger = await _offerings.GetLedgerReadOnlyAsync(request.PlayerId, cancellationToken)
                ?? new ClanOfferingLedger(Guid.Empty, request.PlayerId, serverId, now);

            var tree = _technologies.Tree;
            var config = _catalog.Config.Technologies.Clan;

            return new ClanTechnologiesView(
                config.Tabs,
                tree.Tiers.Select(t => new ClanTechnologyTierView(t.Tier, t.WorldLevel, worldLevel >= t.WorldLevel)).ToList(),
                tree.Tiers.SelectMany(tier => tree.Nodes.Select(node => NodeView(research, tier.Tier, node, worldLevel))).ToList(),
                research.UnallocatedPoints,
                ledger.Coins,
                TechnologyBonusTotals.Of(tree.Bonuses(research.Levels), _catalog.Config),
                OfferingView(research, ledger, _status.MainBuildingLevel(village), now),
                DailyView(config, ledger, now),
                await ContributorsAsync(clanId, now, cancellationToken));
        }

        private ClanTechnologyNodeView NodeView(ClanResearch research, int tier, TechnologyNodeConfig node, int worldLevel)
        {
            var key = new TechnologyKey(tier, node.Key);
            var level = research.LevelOf(key);
            var scale = _technologies.Tree.Scale(tier, node.Scaling);

            return new ClanTechnologyNodeView(
                tier,
                node.Key,
                node.Tab,
                node.Row,
                node.Requires,
                level,
                node.Levels,
                // У клану ратуші немає — її гейт дерево пропускає найбільшим рівнем, як і в розподілі очок
                _technologies.Tree.Availability(research.Levels, tier, node.Key, int.MaxValue, worldLevel).ToString(),
                node.Effects.Select(e => new TechnologyEffectView(e.Stat, e.Target, e.PerLevel * scale)).ToList(),
                research.ProgressOf(key),
                level >= node.Levels ? null : _technologies.Points(tier, node, level + 1));
        }

        private ClanOfferingStateView OfferingView(ClanResearch research, ClanOfferingLedger ledger, int mainBuildingLevel, DateTime now)
        {
            var offering = _technologies.Offering;
            var recharge = TimeSpan.FromSeconds(offering.AttemptRechargeSeconds);
            var maxAttempts = _technologies.MaxAttempts(research.Levels);
            var resource = _technologies.OfferedResource(research.ResourceOfferings);
            var nextGem = ledger.NextGemOffering(now);

            return new ClanOfferingStateView(
                resource,
                resource is null ? null : _technologies.AttemptCost(resource, mainBuildingLevel),
                _technologies.OfferingsUntilRotation(research.ResourceOfferings),
                ledger.AttemptsAvailable(maxAttempts, recharge, now),
                maxAttempts,
                ledger.NextAttemptAt(recharge, now),
                offering.PointsPerAttempt,
                nextGem > offering.GemDailyLimit ? null : _technologies.GemOfferingPrice(nextGem),
                nextGem - 1,
                offering.GemDailyLimit,
                offering.GemPoints);
        }

        private static ClanDailyContributionView DailyView(ClanTechnologiesConfig config, ClanOfferingLedger ledger, DateTime now)
        {
            var today = ledger.PointsTodayAt(now);

            return new ClanDailyContributionView(today, config.DailyContributionRewards
                .Select(r => new ClanDailyRewardView(r.Points, r.Rewards, today >= r.Points))
                .ToList());
        }

        private async Task<List<ClanContributorView>> ContributorsAsync(Guid clanId, DateTime now, CancellationToken cancellationToken)
        {
            var contributions = await _offerings.GetMemberContributionsAsync(clanId, cancellationToken);

            if (contributions.Count == 0)
                return [];

            var names = await _players.GetNamesAsync(contributions.Select(c => c.PlayerId).ToList(), cancellationToken);

            return contributions
                .Select(c => new ClanContributorView(c.PlayerId, names.GetValueOrDefault(c.PlayerId, "—"), c.TodayAt(now), c.Total))
                .OrderByDescending(c => c.Today)
                .ThenByDescending(c => c.Total)
                .ThenBy(c => c.PlayerId)
                .ToList();
        }
    }
}
