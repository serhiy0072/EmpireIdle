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
    /// Піднімає рівень героя одразу за досвід із пулу гравця (GDD §6.1): без черги, будівлі й таймера.
    /// Стеля одна — MaxLevel; ратуша й тір рівень більше не обмежують.
    /// </summary>
    public record LevelUpHeroCommand(Guid PlayerId, Guid HeroId, int Levels = 1)
        : IRequest, IPlayerScopedRequest, IIdempotentRequest;

    internal sealed class LevelUpHeroCommandHandler : IRequestHandler<LevelUpHeroCommand>
    {
        private readonly IHeroRepository _heroRepository;
        private readonly IUnitOfWork _unitOfWork;
        private readonly HeroProgression _progression;
        private readonly GameCatalog _catalog;
        private readonly TimeProvider _timeProvider;
        private readonly ILogger<LevelUpHeroCommandHandler> _logger;

        public LevelUpHeroCommandHandler(
            IHeroRepository heroRepository,
            IUnitOfWork unitOfWork,
            HeroProgression progression,
            GameCatalog catalog,
            TimeProvider timeProvider,
            ILogger<LevelUpHeroCommandHandler> logger)
        {
            _heroRepository = heroRepository;
            _unitOfWork = unitOfWork;
            _progression = progression;
            _catalog = catalog;
            _timeProvider = timeProvider;
            _logger = logger;
        }

        public async Task Handle(LevelUpHeroCommand request, CancellationToken cancellationToken)
        {
            var now = _timeProvider.GetUtcNow().UtcDateTime;

            var hero = await _heroRepository.GetByIdAsync(request.HeroId, cancellationToken)
                ?? throw new EntityNotFoundException("Hero", request.HeroId.ToString());

            if (hero.PlayerId != request.PlayerId)
                throw new EntityNotFoundException("Hero", request.HeroId.ToString());

            var target = hero.Level + request.Levels;

            if (target > _progression.MaxLevel)
                throw new RequirementNotMetException(RefusalReasons.HeroLevelCeiling,
                    $"Hero {hero.Id} cannot go above level {_progression.MaxLevel}.",
                    _catalog.FindHero(hero.HeroKey)?.DisplayName ?? hero.HeroKey, _progression.MaxLevel);

            var cost = _progression.ExperienceBetween(hero.Level, target);

            // Пул не створюємо: без досвіду піднімати нічим, і відмова має назвати нестачу
            var pool = await _heroRepository.GetExperienceAsync(request.PlayerId, cancellationToken)
                ?? throw new NotEnoughResourcesException("heroExperience", cost, 0);

            pool.Spend(cost);
            hero.GainLevels(request.Levels, _progression.MaxLevel, now);

            await _unitOfWork.SaveChangesAsync(cancellationToken);

            _logger.LogInformation("Player {PlayerId} raised hero {HeroId} to level {Level} for {Experience} experience",
                request.PlayerId, hero.Id, hero.Level, cost);
        }
    }
}
