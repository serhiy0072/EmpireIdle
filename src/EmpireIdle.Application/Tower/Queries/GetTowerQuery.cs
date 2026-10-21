using EmpireIdle.Application.Common.Security;
using EmpireIdle.Application.Common.Services;
using EmpireIdle.Application.Interfaces;
using EmpireIdle.Application.Tower.ReadModels;
using EmpireIdle.Domain.Enums;
using EmpireIdle.Domain.Exceptions;
using EmpireIdle.Domain.Services;
using EmpireIdle.Domain.Tower;
using MediatR;

namespace EmpireIdle.Application.Tower.Queries
{
    /// <summary>Екран вежі: де гравець, хто чекає на наступному поверсі й що дадуть найближчі поверхи.</summary>
    public record GetTowerQuery(Guid PlayerId) : IRequest<TowerView>, IPlayerScopedRequest;

    public sealed class GetTowerQueryHandler : IRequestHandler<GetTowerQuery, TowerView>
    {
        /// <summary>Скільки поверхів показує стрічка: наступний і ще дев'ять — до найближчого боса.</summary>
        private const int UpcomingFloors = 10;

        private readonly ITowerRepository _tower;
        private readonly IVillageRepository _villages;
        private readonly TowerFloors _floors;
        private readonly TowerRewards _rewards;
        private readonly EffectResolver _effects;
        private readonly GameCatalog _catalog;
        private readonly TimeProvider _timeProvider;

        public GetTowerQueryHandler(ITowerRepository tower, IVillageRepository villages, TowerFloors floors, TowerRewards rewards,
            EffectResolver effects, GameCatalog catalog, TimeProvider timeProvider)
        {
            _tower = tower;
            _villages = villages;
            _floors = floors;
            _rewards = rewards;
            _effects = effects;
            _catalog = catalog;
            _timeProvider = timeProvider;
        }

        public async Task<TowerView> Handle(GetTowerQuery request, CancellationToken cancellationToken)
        {
            var now = _timeProvider.GetUtcNow().UtcDateTime;
            var settings = _catalog.Config.Tower;

            var village = await _villages.GetByPlayerIdReadOnlyAsync(request.PlayerId, cancellationToken)
                ?? throw new EntityNotFoundException("Village for player", request.PlayerId);

            var mainLevel = village.Buildings.FirstOrDefault(b => b.Type == _catalog.MainBuildingKey)?.Level.Value ?? 0;
            var progress = await _tower.GetProgressReadOnlyAsync(request.PlayerId, cancellationToken);
            var highest = progress?.HighestFloor ?? 0;
            var rank = progress is { HighestFloor: > 0 } ? await _tower.GetRankAsync(progress, cancellationToken) : (int?)null;

            var upcoming = Enumerable.Range(highest + 1, Math.Max(0, Math.Min(UpcomingFloors, settings.MaxFloor - highest)))
                .Select(number => new TowerUpcomingFloor(number, number % settings.BossEvery == 0, _rewards.For(number)))
                .ToList();

            return new TowerView(
                mainLevel >= settings.RequiresMainBuildingLevel,
                settings.RequiresMainBuildingLevel,
                settings.MaxFloor,
                highest,
                rank,
                highest < settings.MaxFloor ? Floor(highest + 1) : null,
                upcoming,
                await _effects.GetMultiplierAsync(request.PlayerId, EffectTarget.Attack, now, cancellationToken),
                await _effects.GetMultiplierAsync(request.PlayerId, EffectTarget.Defense, now, cancellationToken));
        }

        private TowerFloorView Floor(int number)
        {
            var floor = _floors.Describe(number);

            return new TowerFloorView(
                floor.Number,
                floor.IsBoss,
                floor.Archetype.Key,
                floor.Archetype.DisplayName,
                floor.SetStep.DisplayName,
                floor.HeroLevel,
                floor.UnitLevel,
                floor.Enemies.Select(e => new TowerEnemyView(e.Lane, e.Hero.Key, e.Hero.Class, e.Boss)).ToList(),
                _rewards.For(number));
        }
    }
}
