using EmpireIdle.Application.Common.Security;
using EmpireIdle.Application.Heroes.Contracts;
using EmpireIdle.Application.Interfaces;
using EmpireIdle.Domain.Services;
using MediatR;

namespace EmpireIdle.Application.Heroes.Queries
{
    /// <summary>
    /// Ростер гравця разом зі стелею рівня, уламками й активною чергою.
    /// </summary>
    public record GetHeroesOverviewQuery(Guid PlayerId) : IRequest<HeroesOverview>, IPlayerScopedRequest;

    internal sealed class GetHeroesOverviewQueryHandler : IRequestHandler<GetHeroesOverviewQuery, HeroesOverview>
    {
        private readonly IHeroRepository _heroRepository;
        private readonly IVillageRepository _villageRepository;
        private readonly HeroProgression _progression;
        private readonly GameCatalog _catalog;

        public GetHeroesOverviewQueryHandler(
            IHeroRepository heroRepository,
            IVillageRepository villageRepository,
            HeroProgression progression,
            GameCatalog catalog)
        {
            _heroRepository = heroRepository;
            _villageRepository = villageRepository;
            _progression = progression;
            _catalog = catalog;
        }

        public async Task<HeroesOverview> Handle(GetHeroesOverviewQuery request, CancellationToken cancellationToken)
        {
            var heroes = await _heroRepository.GetByPlayerAsync(request.PlayerId, cancellationToken);

            var village = await _villageRepository.GetByPlayerIdAsync(request.PlayerId, cancellationToken)
                ?? throw new InvalidOperationException($"Village not found for player {request.PlayerId}.");

            // Ратуша в процесі будівництва стелі не піднімає — береться рівень,
            // який уже стоїть. FirstOrDefault дає 0, і стеля чесно стає нулем.
            var townHallLevel = village.Buildings
                .Where(b => b.Type == _catalog.MainBuildingKey && !b.IsUnderConstruction)
                .Select(b => b.Level.Value)
                .FirstOrDefault();

            var summaries = heroes
                .Select(h => new HeroSummary(
                    h.Id,
                    h.HeroKey,
                    h.Tier,
                    h.Level,
                    _progression.MaxLevel(townHallLevel, h.Tier),
                    h.Constellation,
                    h.State.ToString().ToLowerInvariant(),
                    h.HealedAt))
                .ToList();

            var shards = await _heroRepository.GetAllShardsAsync(request.PlayerId, cancellationToken);

            // Уламки показуються лише для тих, кого взагалі можна призвати:
            // решта приходить із банерів цілими
            var shardSummaries = shards
                .Where(s => _catalog.FindHero(s.HeroKey)?.SummonShards > 0)
                .Select(s => new HeroShardSummary(s.HeroKey, s.Count, _catalog.Hero(s.HeroKey).SummonShards))
                .ToList();

            var order = await _heroRepository.GetActiveOrderAsync(request.PlayerId, cancellationToken);

            return new HeroesOverview(
                summaries,
                shardSummaries,
                order is null
                    ? null
                    : new HeroLevelOrderSummary(order.Id, order.HeroId, order.TargetLevel, order.CompletesAt),
                _progression.MarchCapacity(heroes.Count));
        }
    }
}
