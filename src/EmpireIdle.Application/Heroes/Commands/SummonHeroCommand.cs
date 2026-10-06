using EmpireIdle.Application.Common.Security;
using EmpireIdle.Application.Common.Services;
using EmpireIdle.Application.Interfaces;
using EmpireIdle.Domain.Exceptions;
using EmpireIdle.Domain.Services;
using MediatR;
using Microsoft.Extensions.Logging;

namespace EmpireIdle.Application.Heroes.Commands
{
    /// <summary>
    /// Призов героя за накопичені осколки — 10 осколків це герой (GDD §6.1). Окремо від здобуття
    /// навмисно: гравець сам вирішує, коли витратити набране.
    /// </summary>
    public record SummonHeroCommand(Guid PlayerId, string HeroKey)
        : IRequest, IPlayerScopedRequest, IIdempotentRequest;

    internal sealed class SummonHeroCommandHandler : IRequestHandler<SummonHeroCommand>
    {
        private readonly IHeroRepository _heroRepository;
        private readonly IServerRepository _serverRepository;
        private readonly IServerContext _serverContext;
        private readonly HeroGranter _heroGranter;
        private readonly IUnitOfWork _unitOfWork;
        private readonly TimeProvider _timeProvider;
        private readonly ILogger<SummonHeroCommandHandler> _logger;
        private readonly GameCatalog _catalog;

        public SummonHeroCommandHandler(
            IHeroRepository heroRepository,
            IServerRepository serverRepository,
            IServerContext serverContext,
            HeroGranter heroGranter,
            IUnitOfWork unitOfWork,
            TimeProvider timeProvider,
            ILogger<SummonHeroCommandHandler> logger,
            GameCatalog catalog)
        {
            _heroRepository = heroRepository;
            _serverRepository = serverRepository;
            _serverContext = serverContext;
            _heroGranter = heroGranter;
            _unitOfWork = unitOfWork;
            _timeProvider = timeProvider;
            _logger = logger;
            _catalog = catalog;
        }

        public async Task Handle(SummonHeroCommand request, CancellationToken cancellationToken)
        {
            var now = _timeProvider.GetUtcNow().UtcDateTime;

            var config = _catalog.FindHero(request.HeroKey)
                ?? throw new EntityNotFoundException("Hero", request.HeroKey);

            var cost = _catalog.Config.HeroSettings.SummonShards;

            // Герой тіру N приходить лише зі світу N (GDD §6.1), хоч би звідки взялись осколки
            if (await _serverRepository.GetLevelAsync(_serverContext.ServerId, cancellationToken) < config.NativeTier)
                throw new RequirementNotMetException(RefusalReasons.HeroTierLocked,
                    $"Hero '{request.HeroKey}' of tier {config.NativeTier} opens at world level {config.NativeTier}.", config.NativeTier);

            var progress = await _heroRepository.GetShardsAsync(request.PlayerId, request.HeroKey, cancellationToken)
                ?? throw new RequirementNotMetException(RefusalReasons.HeroNotEnoughShards,
                    $"No shards of '{request.HeroKey}' collected yet.", config.DisplayName, cost, 0);

            if (!progress.TryConsume(cost))
                throw new RequirementNotMetException(RefusalReasons.HeroNotEnoughShards,
                    $"Summoning '{request.HeroKey}' needs {cost} shards, {progress.Count} collected.",
                    config.DisplayName, cost, progress.Count);

            await _heroGranter.GrantAsync(request.PlayerId, request.HeroKey, "shard-summon", now, cancellationToken);

            await _unitOfWork.SaveChangesAsync(cancellationToken);

            _logger.LogInformation(
                "Player {PlayerId} summoned {HeroKey} for {Cost} shards, {Remaining} left",
                request.PlayerId, request.HeroKey, cost, progress.Count);
        }
    }
}
