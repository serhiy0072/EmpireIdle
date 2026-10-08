using EmpireIdle.Application.Common.Security;
using EmpireIdle.Application.Heroes.Contracts;
using EmpireIdle.Application.Interfaces;
using EmpireIdle.Domain.Services;
using MediatR;

namespace EmpireIdle.Application.Heroes.Queries
{
    /// <summary>
    /// Ростер гравця разом зі стелею рівня, ціною наступного рівня, уламками, пулом досвіду й табором.
    /// </summary>
    public record GetHeroesOverviewQuery(Guid PlayerId) : IRequest<HeroesOverview>, IPlayerScopedRequest;

    internal sealed class GetHeroesOverviewQueryHandler : IRequestHandler<GetHeroesOverviewQuery, HeroesOverview>
    {
        private readonly IHeroRepository _heroRepository;
        private readonly HeroProgression _progression;
        private readonly HeroSkills _skills;
        private readonly TrainingCampRules _campRules;
        private readonly IVillageRepository _villages;
        private readonly VillageStatus _status;
        private readonly GameCatalog _catalog;
        private readonly TimeProvider _timeProvider;

        public GetHeroesOverviewQueryHandler(
            IHeroRepository heroRepository,
            HeroProgression progression,
            HeroSkills skills,
            TrainingCampRules campRules,
            IVillageRepository villages,
            VillageStatus status,
            GameCatalog catalog,
            TimeProvider timeProvider)
        {
            _heroRepository = heroRepository;
            _progression = progression;
            _skills = skills;
            _campRules = campRules;
            _villages = villages;
            _status = status;
            _catalog = catalog;
            _timeProvider = timeProvider;
        }

        public async Task<HeroesOverview> Handle(GetHeroesOverviewQuery request, CancellationToken cancellationToken)
        {
            var heroes = await _heroRepository.GetByPlayerReadOnlyAsync(request.PlayerId, cancellationToken);

            var shards = await _heroRepository.GetAllShardsAsync(request.PlayerId, cancellationToken);
            var shardCounts = shards.ToDictionary(s => s.HeroKey, s => s.Count);
            var owned = heroes.Select(h => h.HeroKey).ToHashSet();

            var summaries = heroes
                .Select(h => new HeroSummary(
                    h.Id,
                    h.HeroKey,
                    h.Tier,
                    h.Level,
                    h.EffectiveLevel,
                    h.CampSlot,
                    _progression.MaxLevel,
                    h.Level < _progression.MaxLevel ? _progression.ExperienceToNext(h.Level) : 0,
                    h.StarParts,
                    _progression.NextStarPartCost(h.StarParts),
                    shardCounts.GetValueOrDefault(h.HeroKey),
                    // Ім'я enum як є: клієнт розгалужується за "Idle", а не за "idle"
                    h.State.ToString(),
                    h.StationedGarrisonId,
                    h.IsLeader,
                    // Рівні рахує сервер: відкриття за рівнем героя й стелю зірок клієнт не дублює
                    (_catalog.FindHero(h.HeroKey)?.Skills ?? [])
                        .ToDictionary(s => s.Key, s => _skills.LevelOf(h, s)),
                    _skills.LevelCap(h)))
                .ToList();

            // Осколки ще не призваних героїв — до призову; осколки відкритих ідуть у зірки й стоять у картці героя
            var shardSummaries = shards
                .Where(s => !owned.Contains(s.HeroKey) && _catalog.FindHero(s.HeroKey) is not null)
                .Select(s => new HeroShardSummary(s.HeroKey, s.Count, _catalog.Config.HeroSettings.SummonShards))
                .ToList();

            // Пул лише читаємо: без трекінгу тут нема чого зберігати, а свіжість — та сама
            var experience = await _heroRepository.GetExperienceAsync(request.PlayerId, cancellationToken);

            return new HeroesOverview(
                summaries,
                shardSummaries,
                experience?.Amount ?? 0,
                _progression.MarchCapacity(heroes.Count(h => h.IsAvailable)),
                await CampAsync(request.PlayerId, heroes, cancellationToken));
        }

        /// <summary>Табір очима гравця: рівень, відкриті слоти, хто де стоїть і що перезаряджається.</summary>
        private async Task<TrainingCampView> CampAsync(Guid playerId, IReadOnlyCollection<Domain.Entities.Hero> heroes,
            CancellationToken cancellationToken)
        {
            var now = _timeProvider.GetUtcNow().UtcDateTime;
            var camp = await _heroRepository.GetCampAsync(playerId, cancellationToken);
            var village = await _villages.GetByPlayerIdReadOnlyAsync(playerId, cancellationToken);

            var purchased = camp?.PurchasedSlots ?? 0;
            var free = village is null ? 0 : _campRules.FreeSlots(_status.MainBuildingLevel(village));
            var total = free + purchased;
            var level = _campRules.CampLevel(heroes);

            var slots = Enumerable.Range(0, total)
                .Select(index => new CampSlotView(
                    index,
                    heroes.FirstOrDefault(h => h.CampSlot == index)?.Id,
                    camp is not null && camp.IsCoolingDown(index, now) ? camp.SlotCooldowns[index] : null))
                .ToList();

            return new TrainingCampView(
                level is not null,
                level,
                _campRules.ReferenceSize,
                total,
                free,
                purchased,
                _campRules.MaxPurchasableSlots,
                _campRules.NextSlotPrice(purchased),
                _campRules.SkipCooldownGems,
                slots);
        }
    }
}
