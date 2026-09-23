using EmpireIdle.Application.Common.Security;
using EmpireIdle.Application.Interfaces;
using EmpireIdle.Domain.Exceptions;
using EmpireIdle.Domain.Services;
using MediatR;
using Microsoft.Extensions.Logging;

namespace EmpireIdle.Application.Heroes.Commands
{
    /// <summary>Вилікувати пораненого героя за ресурси.</summary>
    public record HealHeroCommand(Guid PlayerId, Guid HeroId)
        : IRequest, IPlayerScopedRequest, IIdempotentRequest;

    /// <summary>
    /// Обробник HealHeroCommand.
    ///
    /// Лікування миттєве, як і в юнітів: ціна в ресурсах, а не в часі.
    /// Госпіталь при цьому потрібен — без будівлі героя нікому виписувати,
    /// але слота він не займає: місткість вирішує, скільки поранених юнітів
    /// виживе, а герой не гине ніколи.
    /// </summary>
    public sealed class HealHeroCommandHandler : IRequestHandler<HealHeroCommand>
    {
        private readonly IHeroRepository _heroRepository;
        private readonly IVillageRepository _villageRepository;
        private readonly IUnitOfWork _unitOfWork;
        private readonly TimeProvider _timeProvider;
        private readonly HeroProgression _progression;
        private readonly GameCatalog _catalog;
        private readonly ILogger<HealHeroCommandHandler> _logger;

        public HealHeroCommandHandler(
            IHeroRepository heroRepository,
            IVillageRepository villageRepository,
            IUnitOfWork unitOfWork,
            TimeProvider timeProvider,
            HeroProgression progression,
            GameCatalog catalog,
            ILogger<HealHeroCommandHandler> logger)
        {
            _heroRepository = heroRepository;
            _villageRepository = villageRepository;
            _unitOfWork = unitOfWork;
            _timeProvider = timeProvider;
            _progression = progression;
            _catalog = catalog;
            _logger = logger;
        }

        public async Task Handle(HealHeroCommand request, CancellationToken cancellationToken)
        {
            var now = _timeProvider.GetUtcNow().UtcDateTime;

            var hero = await _heroRepository.GetByIdAsync(request.HeroId, cancellationToken)
                ?? throw new EntityNotFoundException("Hero", request.HeroId.ToString());

            if (hero.PlayerId != request.PlayerId)
                throw new EntityNotFoundException("Hero", request.HeroId.ToString());

            var village = await _villageRepository.GetByPlayerIdAsync(request.PlayerId, cancellationToken)
                ?? throw new InvalidOperationException($"Village not found for player {request.PlayerId}.");

            var buildingKey = _catalog.Config.HeroSettings.HealBuildingKey;

            if (!village.HasBuilding(buildingKey))
                throw new RequirementNotMetException(RefusalReasons.BuildingRequired,
                    $"Healing a hero requires the {buildingKey}.", _catalog.Building(buildingKey).DisplayName);

            // Стан перевіряє агрегат: він же не дасть лікувати того, хто в дорозі
            hero.Heal(now);

            village.ChargeCost(_progression.HealCost(hero.Level), now);

            await _unitOfWork.SaveChangesAsync(cancellationToken);

            _logger.LogInformation("Hero {HeroId} healed for player {PlayerId}", hero.Id, request.PlayerId);
        }
    }
}
