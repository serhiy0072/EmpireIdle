using EmpireIdle.Application.Common.Security;
using EmpireIdle.Application.Dungeons.Contracts;
using EmpireIdle.Application.Dungeons.Services;
using EmpireIdle.Application.Interfaces;
using EmpireIdle.Domain.Dungeons;
using EmpireIdle.Domain.Services;
using MediatR;

namespace EmpireIdle.Application.Dungeons.Queries
{
    /// <summary>Вітрина данжів із енергією, гейтами й прогресом гравця.</summary>
    public record GetDungeonsQuery(Guid PlayerId) : IRequest<DungeonsOverview>, IPlayerScopedRequest;

    internal sealed class GetDungeonsQueryHandler : IRequestHandler<GetDungeonsQuery, DungeonsOverview>
    {
        private readonly IDungeonRepository _dungeons;
        private readonly IVillageRepository _villages;
        private readonly BattleBuilder _builder;
        private readonly GameCatalog _catalog;
        private readonly TimeProvider _timeProvider;

        public GetDungeonsQueryHandler(
            IDungeonRepository dungeons,
            IVillageRepository villages,
            BattleBuilder builder,
            GameCatalog catalog,
            TimeProvider timeProvider)
        {
            _dungeons = dungeons;
            _villages = villages;
            _builder = builder;
            _catalog = catalog;
            _timeProvider = timeProvider;
        }

        public async Task<DungeonsOverview> Handle(GetDungeonsQuery request, CancellationToken cancellationToken)
        {
            var now = _timeProvider.GetUtcNow().UtcDateTime;
            var settings = _catalog.Config.Dungeons;

            var village = await _villages.GetByPlayerIdReadOnlyAsync(request.PlayerId, cancellationToken);

            var mainLevel = village?.Buildings
                .FirstOrDefault(b => b.Type == _catalog.MainBuildingKey)?.Level.Value ?? 0;

            var cleared = await _dungeons.GetClearedLevelsAsync(request.PlayerId, cancellationToken);
            var energy = await _dungeons.GetEnergyAsync(request.PlayerId, cancellationToken);
            var run = await _dungeons.GetActiveRunAsync(request.PlayerId, cancellationToken);

            // Гравець, який ще не заходив, бачить повну шкалу — саме стільки він і отримає
            var current = energy?.Current(settings.MaxEnergy, settings.RegenHours, now) ?? settings.MaxEnergy;
            var fullAt = energy?.FullAt(settings.MaxEnergy, settings.RegenHours, now);

            var views = settings.Dungeons
                .Select(dungeon =>
                {
                    var clearedLevel = cleared.GetValueOrDefault(dungeon.Key);

                    var levels = Enumerable.Range(1, settings.MaxLevel)
                        .Select(level => new DungeonLevelView(
                            level,
                            DungeonRewarder.RarityFor(level).ToString().ToLowerInvariant(),
                            _builder.WaveCount(level),
                            _builder.PowerMultiplier(level),
                            dungeon.Reward
                                .Select(r => new RewardLine(r.Resource, (int)Math.Round(r.Amount * _builder.RewardMultiplier(level))))
                                .ToList()))
                        .ToList();

                    return new DungeonView(
                        dungeon.Key,
                        dungeon.DisplayName,
                        dungeon.Description,
                        dungeon.ArtifactSetKey,
                        dungeon.RequiresMainBuildingLevel,
                        mainLevel >= dungeon.RequiresMainBuildingLevel,
                        clearedLevel,
                        Math.Min(settings.MaxLevel, clearedLevel + 1),
                        levels);
                })
                .ToList();

            return new DungeonsOverview(
                current,
                settings.MaxEnergy,
                settings.EnergyPerRun,
                fullAt,
                settings.TeamSize,
                run?.Id,
                views);
        }
    }
}
