using EmpireIdle.Application.Common.Security;
using EmpireIdle.Application.Heroes.Contracts;
using EmpireIdle.Application.Interfaces;
using EmpireIdle.Domain.Services;
using MediatR;

namespace EmpireIdle.Application.Heroes.Queries
{
    /// <summary>
    /// Ростер гравця разом зі стелею рівня, ціною наступного рівня, уламками й пулом досвіду.
    /// </summary>
    public record GetHeroesOverviewQuery(Guid PlayerId) : IRequest<HeroesOverview>, IPlayerScopedRequest;

    internal sealed class GetHeroesOverviewQueryHandler : IRequestHandler<GetHeroesOverviewQuery, HeroesOverview>
    {
        private readonly IHeroRepository _heroRepository;
        private readonly HeroProgression _progression;
        private readonly GameCatalog _catalog;

        public GetHeroesOverviewQueryHandler(
            IHeroRepository heroRepository,
            HeroProgression progression,
            GameCatalog catalog)
        {
            _heroRepository = heroRepository;
            _progression = progression;
            _catalog = catalog;
        }

        public async Task<HeroesOverview> Handle(GetHeroesOverviewQuery request, CancellationToken cancellationToken)
        {
            var heroes = await _heroRepository.GetByPlayerReadOnlyAsync(request.PlayerId, cancellationToken);

            var summaries = heroes
                .Select(h => new HeroSummary(
                    h.Id,
                    h.HeroKey,
                    h.Tier,
                    h.Level,
                    _progression.MaxLevel,
                    h.Level < _progression.MaxLevel ? _progression.ExperienceToNext(h.Level) : 0,
                    h.Constellation,
                    // Ім'я enum як є: клієнт розгалужується за "Idle", а не за "idle"
                    h.State.ToString(),
                    h.StationedGarrisonId,
                    h.IsLeader))
                .ToList();

            var shards = await _heroRepository.GetAllShardsAsync(request.PlayerId, cancellationToken);

            // Уламки показуються лише для тих, кого взагалі можна призвати:
            // решта приходить із банерів цілими
            var shardSummaries = shards
                .Where(s => _catalog.FindHero(s.HeroKey)?.SummonShards > 0)
                .Select(s => new HeroShardSummary(s.HeroKey, s.Count, _catalog.Hero(s.HeroKey).SummonShards))
                .ToList();

            // Пул лише читаємо: без трекінгу тут нема чого зберігати, а свіжість — та сама
            var experience = await _heroRepository.GetExperienceAsync(request.PlayerId, cancellationToken);

            return new HeroesOverview(
                summaries,
                shardSummaries,
                experience?.Amount ?? 0,
                _progression.MarchCapacity(heroes.Count(h => h.IsAvailable)));
        }
    }
}
