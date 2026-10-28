using EmpireIdle.Application.Common.Security;
using EmpireIdle.Application.Interfaces;
using EmpireIdle.Application.Research.ReadModels;
using EmpireIdle.Domain.Services;
using EmpireIdle.Domain.Services.Config;
using EmpireIdle.Domain.Technologies;
using MediatR;

namespace EmpireIdle.Application.Research.Queries
{
    /// <summary>Академія гравця: дерево з рівнями, ціною й часом наступного рівня та активне дослідження.</summary>
    public record GetResearchQuery(Guid PlayerId) : IRequest<ResearchView>, IPlayerScopedRequest;

    public sealed class GetResearchQueryHandler : IRequestHandler<GetResearchQuery, ResearchView>
    {
        private readonly IVillageRepository _villages;
        private readonly IPlayerResearchRepository _research;
        private readonly IServerRepository _servers;
        private readonly IServerContext _serverContext;
        private readonly PersonalTechnologies _technologies;
        private readonly VillageStatus _status;
        private readonly GameCatalog _catalog;
        private readonly TimeProvider _timeProvider;

        public GetResearchQueryHandler(IVillageRepository villages, IPlayerResearchRepository research, IServerRepository servers,
            IServerContext serverContext, PersonalTechnologies technologies, VillageStatus status, GameCatalog catalog, TimeProvider timeProvider)
        {
            _villages = villages;
            _research = research;
            _servers = servers;
            _serverContext = serverContext;
            _technologies = technologies;
            _status = status;
            _catalog = catalog;
            _timeProvider = timeProvider;
        }

        public async Task<ResearchView> Handle(GetResearchQuery request, CancellationToken cancellationToken)
        {
            var now = _timeProvider.GetUtcNow().UtcDateTime;
            var village = await _villages.GetByPlayerIdReadOnlyAsync(request.PlayerId, cancellationToken)
                ?? throw new InvalidOperationException($"Village not found for player {request.PlayerId}.");
            var research = await _research.GetByPlayerReadOnlyAsync(request.PlayerId, cancellationToken);
            var server = await _servers.GetByIdAsync(_serverContext.ServerId, cancellationToken);

            var mainBuildingLevel = _status.MainBuildingLevel(village);
            var worldLevel = server?.Level ?? 1;
            var academy = _technologies.BuildingKey is { } key ? _catalog.Buildings.GetValueOrDefault(key) : null;

            // Запит нічого не змінює: дозріле, але ще не завершене сканером дослідження показується вже вивченим
            var levels = new Dictionary<string, int>(research?.Levels ?? new Dictionary<string, int>());
            var active = research is { ActiveKey: { } activeKey, CompletesAt: { } completesAt } ? (Key: activeKey, CompletesAt: completesAt) : default;
            var ripe = active.Key is not null && active.CompletesAt <= now;

            if (ripe)
                levels[active.Key!] = research!.ActiveLevel;

            var tree = _technologies.Tree;
            var bonuses = tree.Bonuses(levels);
            var speed = 1 + bonuses.Of(TechnologyStat.ResearchSpeed);

            var nodes = tree.Tiers
                .SelectMany(tier => tree.Nodes.Select(node => NodeView(levels, tier.Tier, node, mainBuildingLevel, worldLevel, speed)))
                .ToList();

            var activeView = research is { ActiveKey: { } current, StartedAt: { } startedAt, CompletesAt: { } completes } && !ripe
                && TechnologyKey.Parse(current) is { } parsed
                    ? new ActiveResearchView(parsed.Tier, parsed.Node, research.ActiveLevel, startedAt, completes,
                        (int)Math.Round(research.PlannedDuration.TotalSeconds))
                    : null;

            return new ResearchView(
                research?.Id,
                academy is not null && _status.IsUnlocked(village, academy.Key),
                academy?.RequiresMainBuildingLevel ?? 0,
                _catalog.Config.Technologies.Personal.Tabs,
                tree.Tiers.Select(t => new ResearchTierView(t.Tier, t.MainBuildingLevel, t.WorldLevel,
                    mainBuildingLevel >= t.MainBuildingLevel && worldLevel >= t.WorldLevel)).ToList(),
                nodes,
                activeView,
                Totals(bonuses));
        }

        private ResearchNodeView NodeView(IReadOnlyDictionary<string, int> levels, int tier, TechnologyNodeConfig node,
            int mainBuildingLevel, int worldLevel, double speed)
        {
            var level = TechnologyTree.LevelOf(levels, tier, node.Key);
            var maxed = level >= node.Levels;
            var scale = _technologies.Tree.Scale(tier, node.Scaling);

            return new ResearchNodeView(
                tier,
                node.Key,
                node.Tab,
                node.Row,
                node.Requires,
                level,
                node.Levels,
                _technologies.Tree.Availability(levels, tier, node.Key, mainBuildingLevel, worldLevel).ToString(),
                node.Effects.Select(e => new TechnologyEffectView(e.Stat, e.Target, e.PerLevel * scale)).ToList(),
                maxed ? null : _technologies.Cost(tier, node, level + 1),
                maxed ? null : (int)Math.Round(_technologies.Duration(tier, node, level + 1).TotalSeconds / speed));
        }

        /// <summary>Сума за статами й цілями, лише ненульові — те, що екран показує як «що вже дають технології».</summary>
        private List<TechnologyBonusView> Totals(TechnologyBonuses bonuses)
        {
            var targets = new List<string?> { null };
            targets.AddRange(_catalog.Config.Units.Select(u => u.Key));
            targets.AddRange(_catalog.Config.Resources.Select(r => r.Key));

            return Enum.GetValues<TechnologyStat>()
                .SelectMany(stat => targets.Select(target => (stat, target,
                    value: target is null ? bonuses.Of(stat) : bonuses.Of(stat, target) - bonuses.Of(stat))))
                .Where(x => Math.Abs(x.value) > 1e-9)
                .Select(x => new TechnologyBonusView(x.stat, x.target, x.value))
                .ToList();
        }
    }
}
