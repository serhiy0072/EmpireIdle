using EmpireIdle.Application.Battles.ReadModels;
using EmpireIdle.Application.Common.Security;
using EmpireIdle.Application.Interfaces;
using EmpireIdle.Application.Tower.Services;
using EmpireIdle.Domain.Battles;
using EmpireIdle.Domain.Entities;
using EmpireIdle.Domain.Services;
using MediatR;

namespace EmpireIdle.Application.Tower.Queries
{
    /// <summary>
    /// Ручний бій на поверсі (рішення 19.10.2026): кожне натискання вміння перераховує бій з нуля з тим самим
    /// сідом і всіма наказами — до натискання бій той самий, тож клієнт продовжує з того ж місця. Нічого не
    /// зберігає: результат зараховує лише штурм із фінальним списком наказів.
    /// </summary>
    /// <param name="Casts">Усі накази гравця від початку бою.</param>
    public record PreviewTowerFloorQuery(Guid PlayerId, int Floor, IReadOnlyList<Guid?> Lanes, IReadOnlyList<ManualCast> Casts)
        : IRequest<BattleReplay>, IPlayerScopedRequest;

    public sealed class PreviewTowerFloorQueryHandler : IRequestHandler<PreviewTowerFloorQuery, BattleReplay>
    {
        private readonly ITowerRepository _tower;
        private readonly TowerBattle _battles;
        private readonly GameCatalog _catalog;
        private readonly IServerContext _serverContext;
        private readonly TimeProvider _timeProvider;

        public PreviewTowerFloorQueryHandler(ITowerRepository tower, TowerBattle battles, GameCatalog catalog, IServerContext serverContext,
            TimeProvider timeProvider)
        {
            _tower = tower;
            _battles = battles;
            _catalog = catalog;
            _serverContext = serverContext;
            _timeProvider = timeProvider;
        }

        public async Task<BattleReplay> Handle(PreviewTowerFloorQuery request, CancellationToken cancellationToken)
        {
            await _battles.EnsureUnlockedAsync(request.PlayerId, cancellationToken);

            // Ті самі ворота, що й у штурму: закритий поверх не переглядають
            var progress = await _tower.GetProgressReadOnlyAsync(request.PlayerId, cancellationToken)
                ?? new TowerProgress(Guid.NewGuid(), request.PlayerId, _serverContext.ServerId);
            progress.EnsureCanStorm(request.Floor, _catalog.Config.Tower.MaxFloor);

            var battle = await _battles.FightAsync(request.PlayerId, request.Floor, request.Lanes, request.Casts,
                _timeProvider.GetUtcNow().UtcDateTime, cancellationToken);

            return BattleReplay.From(battle, _catalog.Config.RealtimeBattle.TickMilliseconds);
        }
    }
}
