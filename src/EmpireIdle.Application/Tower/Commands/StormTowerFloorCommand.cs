using EmpireIdle.Application.Battles.ReadModels;
using EmpireIdle.Application.Common.Security;
using EmpireIdle.Application.Interfaces;
using EmpireIdle.Application.Rewards;
using EmpireIdle.Application.Tower.Services;
using EmpireIdle.Domain.Battles;
using EmpireIdle.Domain.Entities;
using EmpireIdle.Domain.Services;
using EmpireIdle.Domain.Services.Config;
using EmpireIdle.Domain.Tower;
using MediatR;
using Microsoft.Extensions.Logging;

namespace EmpireIdle.Application.Tower.Commands
{
    /// <summary>
    /// Штурм поверху «Тисячної вежі»: сервер рахує бій до кінця й одразу видає наслідки (рішення 18.10.2026).
    /// Спроби безлімітні й безплатні — програш нічого не забирає й нічого не зберігає.
    /// </summary>
    /// <param name="Lanes">Герої за доріжками: індекс — доріжка, null — порожня.</param>
    /// <param name="Casts">Накази ручного бою, які гравець дав у перегляді; null — авто.</param>
    public record StormTowerFloorCommand(Guid PlayerId, int Floor, IReadOnlyList<Guid?> Lanes, IReadOnlyList<ManualCast>? Casts = null)
        : IRequest<TowerStormResult>, IPlayerScopedRequest, IIdempotentRequest;

    /// <param name="Rewards">Видана нагорода; після поразки — порожньо.</param>
    /// <param name="Battle">Лог бою для відтворення.</param>
    public record TowerStormResult(bool Victory, int Floor, IReadOnlyList<RewardConfig> Rewards, BattleReplay Battle);

    internal sealed class StormTowerFloorCommandHandler : IRequestHandler<StormTowerFloorCommand, TowerStormResult>
    {
        private readonly ITowerRepository _tower;
        private readonly TowerBattle _battles;
        private readonly TowerRewards _rewards;
        private readonly RewardDispatcher _dispatcher;
        private readonly GameCatalog _catalog;
        private readonly IServerContext _serverContext;
        private readonly IUnitOfWork _unitOfWork;
        private readonly TimeProvider _timeProvider;
        private readonly ILogger<StormTowerFloorCommandHandler> _logger;

        public StormTowerFloorCommandHandler(
            ITowerRepository tower,
            TowerBattle battles,
            TowerRewards rewards,
            RewardDispatcher dispatcher,
            GameCatalog catalog,
            IServerContext serverContext,
            IUnitOfWork unitOfWork,
            TimeProvider timeProvider,
            ILogger<StormTowerFloorCommandHandler> logger)
        {
            _tower = tower;
            _battles = battles;
            _rewards = rewards;
            _dispatcher = dispatcher;
            _catalog = catalog;
            _serverContext = serverContext;
            _unitOfWork = unitOfWork;
            _timeProvider = timeProvider;
            _logger = logger;
        }

        public async Task<TowerStormResult> Handle(StormTowerFloorCommand request, CancellationToken cancellationToken)
        {
            var now = _timeProvider.GetUtcNow().UtcDateTime;
            var settings = _catalog.Config.Tower;

            await _battles.EnsureUnlockedAsync(request.PlayerId, cancellationToken);

            var progress = await _tower.GetProgressAsync(request.PlayerId, cancellationToken);
            var fresh = progress is null;
            progress ??= new TowerProgress(Guid.NewGuid(), request.PlayerId, _serverContext.ServerId);

            // До бою: поверх, який уже закрито в іншій вкладці, не варто навіть рахувати
            progress.EnsureCanStorm(request.Floor, settings.MaxFloor);

            var battle = await _battles.FightAsync(request.PlayerId, request.Floor, request.Lanes, request.Casts, now, cancellationToken);

            if (battle.Winner != BattleSide.Attacker)
            {
                _logger.LogInformation("Player {PlayerId} fell on tower floor {Floor} after {Ticks} ticks",
                    request.PlayerId, request.Floor, battle.Ticks);

                return new TowerStormResult(false, request.Floor, [], Replay(battle));
            }

            progress.Clear(request.Floor, settings.MaxFloor, now);

            if (fresh)
                await _tower.AddProgressAsync(progress, cancellationToken);

            var rewards = _rewards.For(request.Floor);
            await _dispatcher.GrantAllAsync(request.PlayerId, rewards, $"tower:{request.Floor}", now, cancellationToken);
            await _unitOfWork.SaveChangesAsync(cancellationToken);

            _logger.LogInformation("Player {PlayerId} cleared tower floor {Floor} in {Ticks} ticks",
                request.PlayerId, request.Floor, battle.Ticks);

            return new TowerStormResult(true, request.Floor, rewards, Replay(battle));
        }

        private BattleReplay Replay(BattleResult battle) => BattleReplay.From(battle, _catalog.Config.RealtimeBattle.TickMilliseconds);
    }
}
