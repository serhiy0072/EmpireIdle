using EmpireIdle.Application.Common.Security;
using EmpireIdle.Application.Interfaces;
using EmpireIdle.Domain.Entities;
using EmpireIdle.Domain.Enums;
using EmpireIdle.Domain.Exceptions;
using EmpireIdle.Domain.Services;
using MediatR;
using Microsoft.Extensions.Logging;

namespace EmpireIdle.Application.Heroes.Commands
{
    /// <summary>
    /// Ставить героя в чергу на підняття рівня в залі героїв.
    ///
    /// Одна черга на гравця, як і одна будівля. Скасування немає навмисно:
    /// повернення ресурсів перетворило б чергу на безкоштовний резерв.
    /// </summary>
    public record StartHeroLevelUpCommand(Guid PlayerId, Guid HeroId)
        : IRequest, IPlayerScopedRequest, IIdempotentRequest;

    internal sealed class StartHeroLevelUpCommandHandler : IRequestHandler<StartHeroLevelUpCommand>
    {
        private readonly IVillageRepository _villageRepository;
        private readonly IHeroRepository _heroRepository;
        private readonly IUnitOfWork _unitOfWork;
        private readonly HeroProgression _progression;
        private readonly TimeProvider _timeProvider;
        private readonly ILogger<StartHeroLevelUpCommandHandler> _logger;
        private readonly GameCatalog _catalog;

        public StartHeroLevelUpCommandHandler(
            IVillageRepository villageRepository,
            IHeroRepository heroRepository,
            IUnitOfWork unitOfWork,
            HeroProgression progression,
            TimeProvider timeProvider,
            ILogger<StartHeroLevelUpCommandHandler> logger,
            GameCatalog catalog)
        {
            _villageRepository = villageRepository;
            _heroRepository = heroRepository;
            _unitOfWork = unitOfWork;
            _progression = progression;
            _timeProvider = timeProvider;
            _logger = logger;
            _catalog = catalog;
        }

        public async Task Handle(StartHeroLevelUpCommand request, CancellationToken cancellationToken)
        {
            var now = _timeProvider.GetUtcNow().UtcDateTime;
            var settings = _catalog.Config.HeroSettings;

            var hero = await _heroRepository.GetByIdAsync(request.HeroId, cancellationToken)
                ?? throw new EntityNotFoundException("Hero", request.HeroId.ToString());

            if (hero.PlayerId != request.PlayerId)
                throw new EntityNotFoundException("Hero", request.HeroId.ToString());

            // Поранений качається вільно: госпіталь забирає його з карти,
            // але не з зали героїв
            if (hero.State == HeroState.Deployed)
                throw new InvalidStateException($"Hero {hero.Id} is on a march and cannot be trained.");

            var active = await _heroRepository.GetActiveOrderAsync(request.PlayerId, cancellationToken);

            if (active is not null)
                throw new RequirementNotMetException(
                    $"The heroes hall is already training a hero until {active.CompletesAt:u}.");

            var village = await _villageRepository.GetByPlayerIdAsync(request.PlayerId, cancellationToken)
                ?? throw new InvalidOperationException($"Village not found for player {request.PlayerId}.");

            var hall = village.Buildings
                .FirstOrDefault(b => b.Type == settings.BuildingKey && !b.IsUnderConstruction)
                ?? throw new RequirementNotMetException($"Training heroes requires a '{settings.BuildingKey}'.");

            var townHall = village.Buildings
                .FirstOrDefault(b => b.Type == _catalog.MainBuildingKey && !b.IsUnderConstruction)
                ?? throw new RequirementNotMetException("Training heroes requires a completed town hall.");

            var ceiling = _progression.MaxLevel(townHall.Level.Value, hero.Tier);

            if (hero.Level >= ceiling)
                throw new RequirementNotMetException(
                    $"Hero {hero.Id} is at level {hero.Level} of {ceiling}: raise the town hall or evolve the tier.");

            var target = hero.Level + 1;
            var config = _catalog.Hero(hero.HeroKey);

            village.ChargeCost(_progression.LevelUpCost(config, target), now, target);

            var order = new HeroLevelOrder(Guid.NewGuid(), hero.Id, request.PlayerId, hero.ServerId,
                target, now + _progression.LevelUpDuration(target));

            await _heroRepository.AddOrderAsync(order, cancellationToken);

            await _unitOfWork.SaveChangesAsync(cancellationToken);

            _logger.LogInformation(
                "Player {PlayerId} started levelling hero {HeroId} to {Target} in {Building} level {Level}, done at {CompletesAt}",
                request.PlayerId, hero.Id, target, hall.Type, hall.Level.Value, order.CompletesAt);
        }
    }
}
