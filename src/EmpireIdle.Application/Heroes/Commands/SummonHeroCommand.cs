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
    /// Призов героя за накопичені уламки. Окремо від купівлі навмисно:
    /// гравець сам вирішує, коли витратити набране, і може тримати уламки
    /// про запас.
    /// </summary>
    public record SummonHeroCommand(Guid PlayerId, string HeroKey)
        : IRequest, IPlayerScopedRequest, IIdempotentRequest;

    internal sealed class SummonHeroCommandHandler : IRequestHandler<SummonHeroCommand>
    {
        private readonly IHeroRepository _heroRepository;
        private readonly HeroGranter _heroGranter;
        private readonly IUnitOfWork _unitOfWork;
        private readonly TimeProvider _timeProvider;
        private readonly ILogger<SummonHeroCommandHandler> _logger;
        private readonly GameCatalog _catalog;

        public SummonHeroCommandHandler(
            IHeroRepository heroRepository,
            HeroGranter heroGranter,
            IUnitOfWork unitOfWork,
            TimeProvider timeProvider,
            ILogger<SummonHeroCommandHandler> logger,
            GameCatalog catalog)
        {
            _heroRepository = heroRepository;
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

            var progress = await _heroRepository.GetShardsAsync(request.PlayerId, request.HeroKey, cancellationToken)
                ?? throw new RequirementNotMetException($"No shards of '{request.HeroKey}' collected yet.");

            if (!progress.TryConsume(config.SummonShards))
                throw new RequirementNotMetException(
                    $"Summoning '{request.HeroKey}' needs {config.SummonShards} shards, {progress.Count} collected.");

            await _heroGranter.GrantAsync(request.PlayerId, request.HeroKey, "shard-summon", now, cancellationToken);

            await _unitOfWork.SaveChangesAsync(cancellationToken);

            _logger.LogInformation(
                "Player {PlayerId} summoned {HeroKey} for {Cost} shards, {Remaining} left",
                request.PlayerId, request.HeroKey, config.SummonShards, progress.Count);
        }
    }
}
