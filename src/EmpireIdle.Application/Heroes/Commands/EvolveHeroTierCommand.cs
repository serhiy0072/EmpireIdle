using EmpireIdle.Application.Common.Security;
using EmpireIdle.Application.Interfaces;
using EmpireIdle.Domain.Enums;
using EmpireIdle.Domain.Exceptions;
using EmpireIdle.Domain.Services;
using MediatR;
using Microsoft.Extensions.Logging;

namespace EmpireIdle.Application.Heroes.Commands
{
    /// <summary>
    /// Піднімає тір героя за предмет еволюції. Тір прив'язаний до рівня світу:
    /// другий відкривається на сервері 2, третій на сервері 3 — контент
    /// відкривається для всіх одночасно, не для найшвидших.
    /// </summary>
    public record EvolveHeroTierCommand(Guid PlayerId, Guid HeroId)
        : IRequest, IPlayerScopedRequest, IIdempotentRequest;

    internal sealed class EvolveHeroTierCommandHandler : IRequestHandler<EvolveHeroTierCommand>
    {
        private readonly IHeroRepository _heroRepository;
        private readonly IInventoryRepository _inventoryRepository;
        private readonly IServerRepository _serverRepository;
        private readonly IServerContext _serverContext;
        private readonly IUnitOfWork _unitOfWork;
        private readonly HeroProgression _progression;
        private readonly TimeProvider _timeProvider;
        private readonly ILogger<EvolveHeroTierCommandHandler> _logger;
        private readonly GameCatalog _catalog;

        public EvolveHeroTierCommandHandler(
            IHeroRepository heroRepository,
            IInventoryRepository inventoryRepository,
            IServerRepository serverRepository,
            IServerContext serverContext,
            IUnitOfWork unitOfWork,
            HeroProgression progression,
            TimeProvider timeProvider,
            ILogger<EvolveHeroTierCommandHandler> logger,
            GameCatalog catalog)
        {
            _heroRepository = heroRepository;
            _inventoryRepository = inventoryRepository;
            _serverRepository = serverRepository;
            _serverContext = serverContext;
            _unitOfWork = unitOfWork;
            _progression = progression;
            _timeProvider = timeProvider;
            _logger = logger;
            _catalog = catalog;
        }

        public async Task Handle(EvolveHeroTierCommand request, CancellationToken cancellationToken)
        {
            var now = _timeProvider.GetUtcNow().UtcDateTime;
            var settings = _catalog.Config.HeroSettings;

            var hero = await _heroRepository.GetByIdAsync(request.HeroId, cancellationToken)
                ?? throw new EntityNotFoundException("Hero", request.HeroId.ToString());

            if (hero.PlayerId != request.PlayerId)
                throw new EntityNotFoundException("Hero", request.HeroId.ToString());

            if (hero.State == HeroState.Deployed)
                throw new InvalidStateException($"Hero {hero.Id} is on a march and cannot evolve.");

            var server = await _serverRepository.GetByIdAsync(_serverContext.ServerId, cancellationToken)
                ?? throw new InvalidOperationException($"Server {_serverContext.ServerId} not found.");

            if (!_progression.CanEvolve(hero.Tier, server.Level))
                throw new RequirementNotMetException(
                    $"Evolving past tier {hero.Tier} opens at world level {hero.Tier + 1}; this world is {server.Level}.");

            var itemKey = _progression.EvolutionItemKey(hero.Tier)
                ?? throw new InvalidOperationException($"No evolution item configured for tier {hero.Tier}.");

            var item = await _inventoryRepository.GetItemAsync(request.PlayerId, itemKey, cancellationToken)
                ?? throw new RequirementNotMetException($"Evolving this hero requires '{itemKey}'.");

            // Предмет списується до підняття тіру: зворотний порядок
            // лишив би тір піднятим, якби списання впало
            item.Consume(1);

            hero.EvolveTier(settings.MaxTier, now);

            await _unitOfWork.SaveChangesAsync(cancellationToken);

            _logger.LogInformation(
                "Hero {HeroId} evolved to tier {Tier} on world {ServerId}", hero.Id, hero.Tier, server.Level);
        }
    }
}
