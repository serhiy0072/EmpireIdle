using EmpireIdle.Application.Common.Security;
using EmpireIdle.Application.Interfaces;
using EmpireIdle.Domain.Exceptions;
using EmpireIdle.Domain.Services;
using MediatR;
using Microsoft.Extensions.Logging;

namespace EmpireIdle.Application.Marches.Commands
{
    /// <summary>Відкликати табір (§2.5): армія йде додому звичайним маршем.</summary>
    public record RecallCampCommand(Guid PlayerId, Guid MarchId)
        : IRequest, IPlayerScopedRequest, IIdempotentRequest;

    /// <summary>
    /// Обробник RecallCampCommand. Дорога — від клітинки табору до села
    /// там, де воно стоїть зараз, зі швидкістю найповільнішого в колоні.
    /// </summary>
    public sealed class RecallCampCommandHandler : IRequestHandler<RecallCampCommand>
    {
        private readonly IVillageRepository _villageRepository;
        private readonly IGarrisonRepository _garrisonRepository;
        private readonly IMarchRepository _marchRepository;
        private readonly IHeroRepository _heroRepository;
        private readonly IUnitOfWork _unitOfWork;
        private readonly TimeProvider _timeProvider;
        private readonly MarchCalculator _calculator;
        private readonly HeroProgression _progression;
        private readonly GameCatalog _catalog;
        private readonly ILogger<RecallCampCommandHandler> _logger;

        public RecallCampCommandHandler(
            IVillageRepository villageRepository,
            IGarrisonRepository garrisonRepository,
            IMarchRepository marchRepository,
            IHeroRepository heroRepository,
            IUnitOfWork unitOfWork,
            TimeProvider timeProvider,
            MarchCalculator calculator,
            HeroProgression progression,
            GameCatalog catalog,
            ILogger<RecallCampCommandHandler> logger)
        {
            _villageRepository = villageRepository;
            _garrisonRepository = garrisonRepository;
            _marchRepository = marchRepository;
            _heroRepository = heroRepository;
            _unitOfWork = unitOfWork;
            _timeProvider = timeProvider;
            _calculator = calculator;
            _progression = progression;
            _catalog = catalog;
            _logger = logger;
        }

        public async Task Handle(RecallCampCommand request, CancellationToken cancellationToken)
        {
            var now = _timeProvider.GetUtcNow().UtcDateTime;

            var village = await _villageRepository.GetByPlayerIdAsync(request.PlayerId, cancellationToken)
                ?? throw new InvalidOperationException($"Village not found for player {request.PlayerId}.");

            var garrison = await _garrisonRepository.GetByVillageIdAsync(village.Id, cancellationToken)
                ?? throw new InvalidOperationException($"Garrison not found for village {village.Id}.");

            // Шукаємо серед активних походів цього гарнізону — так чужий табір не відкликати
            var marches = await _marchRepository.GetActiveByGarrisonAsync(garrison.Id, cancellationToken);

            var march = marches.FirstOrDefault(m => m.Id == request.MarchId)
                ?? throw new EntityNotFoundException("Active", request.MarchId);

            var hero = march.HeroId is Guid heroId
                ? await _heroRepository.GetByIdAsync(heroId, cancellationToken)
                : null;

            var duration = _calculator.CalculateDuration(
                march.ServerId, march.TargetX, march.TargetY, village.X, village.Y, march.GetUnits(),
                hero is null ? null : _progression.MarchSpeed(_catalog.FindHero(hero.HeroKey)));

            march.BreakCamp(village.X, village.Y, duration, now);

            await _unitOfWork.SaveChangesAsync(cancellationToken);

            _logger.LogInformation("Player {PlayerId} recalled camp {MarchId} from ({X},{Y}), home in {Minutes:F1} min",
                request.PlayerId, march.Id, march.TargetX, march.TargetY, duration.TotalMinutes);
        }
    }
}
