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
    /// Заповнює наступну частинку зірки героя за його осколки (GDD §6.1). Ціна частинки росте
    /// від зірки до зірки; кожна частинка додає бойової міці, повні зірки відкривають пасивки.
    /// </summary>
    public record AdvanceHeroStarCommand(Guid PlayerId, Guid HeroId)
        : IRequest, IPlayerScopedRequest, IIdempotentRequest;

    internal sealed class AdvanceHeroStarCommandHandler : IRequestHandler<AdvanceHeroStarCommand>
    {
        private readonly IHeroRepository _heroRepository;
        private readonly IUnitOfWork _unitOfWork;
        private readonly HeroProgression _progression;
        private readonly GameCatalog _catalog;
        private readonly TimeProvider _timeProvider;
        private readonly ILogger<AdvanceHeroStarCommandHandler> _logger;

        public AdvanceHeroStarCommandHandler(
            IHeroRepository heroRepository,
            IUnitOfWork unitOfWork,
            HeroProgression progression,
            GameCatalog catalog,
            TimeProvider timeProvider,
            ILogger<AdvanceHeroStarCommandHandler> logger)
        {
            _heroRepository = heroRepository;
            _unitOfWork = unitOfWork;
            _progression = progression;
            _catalog = catalog;
            _timeProvider = timeProvider;
            _logger = logger;
        }

        public async Task Handle(AdvanceHeroStarCommand request, CancellationToken cancellationToken)
        {
            var now = _timeProvider.GetUtcNow().UtcDateTime;
            var settings = _catalog.Config.HeroSettings;

            var hero = await _heroRepository.GetByIdAsync(request.HeroId, cancellationToken)
                ?? throw new EntityNotFoundException("Hero", request.HeroId.ToString());

            if (hero.PlayerId != request.PlayerId)
                throw new EntityNotFoundException("Hero", request.HeroId.ToString());

            // Лот продається за силу на момент виставлення
            if (hero.State == HeroState.OnMarket)
                throw new InvalidStateException(RefusalReasons.HeroOnMarket, $"Hero {hero.Id} is on the market.");

            var cost = _progression.NextStarPartCost(hero.StarParts)
                ?? throw new RequirementNotMetException(RefusalReasons.HeroMaxStars,
                    $"Hero {hero.Id} already has every star.", settings.MaxStarParts);

            var name = _catalog.FindHero(hero.HeroKey)?.DisplayName ?? hero.HeroKey;
            var shards = await _heroRepository.GetShardsAsync(request.PlayerId, hero.HeroKey, cancellationToken);

            // Осколки списуються до заповнення: зворотний порядок лишив би частинку, якби списання впало
            if (shards is null || !shards.TryConsume(cost))
                throw new RequirementNotMetException(RefusalReasons.HeroNotEnoughShards,
                    $"The next star part of '{hero.HeroKey}' needs {cost} shards.", name, cost, shards?.Count ?? 0);

            hero.FillStarPart(settings.MaxStarParts, now);

            await _unitOfWork.SaveChangesAsync(cancellationToken);

            _logger.LogInformation("Hero {HeroId} filled star part {Parts} for {Cost} shards", hero.Id, hero.StarParts, cost);
        }
    }
}
