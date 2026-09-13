using EmpireIdle.Application.Common.Security;
using EmpireIdle.Application.Interfaces;
using EmpireIdle.Application.Marches.Services;
using EmpireIdle.Domain.Entities;
using EmpireIdle.Domain.Enums;
using EmpireIdle.Domain.Exceptions;
using EmpireIdle.Domain.Services;
using MediatR;
using Microsoft.Extensions.Logging;

namespace EmpireIdle.Application.Marches.Commands
{
    /// <summary>
    /// Відправити армію до цілі на карті.
    /// </summary>
    public record SendMarchCommand(
        Guid PlayerId,
        MarchTargetType TargetType,
        Guid TargetId,
        Dictionary<string, int> Units,
        Guid HeroId,
        MarchIntent Intent = MarchIntent.Attack) : IRequest<Guid>, IPlayerScopedRequest, IIdempotentRequest;

    /// <summary>
    /// Обробник SendMarchCommand: знімає юнітів із гарнізону,
    /// рахує час дороги й ставить похід у дорогу.
    /// </summary>
    public sealed class SendMarchCommandHandler : IRequestHandler<SendMarchCommand, Guid>
    {
        private readonly IVillageRepository _villageRepository;
        private readonly IGarrisonRepository _garrisonRepository;
        private readonly IMarchRepository _marchRepository;
        private readonly IHeroRepository _heroRepository;
        private readonly IUnitOfWork _unitOfWork;
        private readonly IServerContext _serverContext;
        private readonly TimeProvider _timeProvider;
        private readonly MarchCalculator _calculator;
        private readonly MarchTargetResolver _targets;
        private readonly ReinforcementRules _reinforcementRules;
        private readonly HeroProgression _progression;
        private readonly ILogger<SendMarchCommandHandler> _logger;

        public SendMarchCommandHandler(
            IVillageRepository villageRepository,
            IGarrisonRepository garrisonRepository,
            IMarchRepository marchRepository,
            IHeroRepository heroRepository,
            IUnitOfWork unitOfWork,
            IServerContext serverContext,
            TimeProvider timeProvider,
            MarchCalculator calculator,
            MarchTargetResolver targets,
            ReinforcementRules reinforcementRules,
            HeroProgression progression,
            ILogger<SendMarchCommandHandler> logger)
        {
            _villageRepository = villageRepository;
            _garrisonRepository = garrisonRepository;
            _marchRepository = marchRepository;
            _heroRepository = heroRepository;
            _serverContext = serverContext;
            _unitOfWork = unitOfWork;
            _calculator = calculator;
            _timeProvider = timeProvider;
            _targets = targets;
            _reinforcementRules = reinforcementRules;
            _progression = progression;
            _logger = logger;
        }

        public async Task<Guid> Handle(SendMarchCommand request, CancellationToken cancellationToken)
        {
            var now = _timeProvider.GetUtcNow().UtcDateTime;

            var village = await _villageRepository.GetByPlayerIdAsync(request.PlayerId, cancellationToken)
                ?? throw new InvalidOperationException($"Village not found for player {request.PlayerId}.");

            var garrison = await _garrisonRepository.GetByVillageIdAsync(village.Id, cancellationToken)
                ?? throw new InvalidOperationException($"Garrison not found for village {village.Id}.");

            // Ліміт одночасних походів
            var hero = await _heroRepository.GetByIdAsync(request.HeroId, cancellationToken)
    ?? throw new EntityNotFoundException("Hero", request.HeroId.ToString());

            // Чужий герой не відрізняється від неіснуючого: інакше відповідь
            // підтверджувала б, що такий герой у когось є
            if (hero.PlayerId != request.PlayerId)
                throw new EntityNotFoundException("Hero", request.HeroId.ToString());

            if (!hero.IsAvailable)
                throw new RequirementNotMetException($"Hero {request.HeroId} is {hero.State}.");

            // Герой веде похід звідти, де стоїть. Без цієї перевірки герой
            // із гарнізону союзника телепортувався б додому, лишивши там
            // свій стек без лідера
            if (hero.StationedGarrisonId != garrison.Id)
                throw new RequirementNotMetException($"Hero {request.HeroId} is stationed in another garrison.");

            var active = await _marchRepository.GetActiveByGarrisonAsync(garrison.Id, cancellationToken);

            // Ростер це вільні герої плюс ті, хто вже в дорозі: кожен похід
            // веде свій герой, тож вільний герой і є вільним слотом, а
            // MaxMarches лишається стелею згори
            var available = await _heroRepository.CountAvailableAsync(request.PlayerId, garrison.Id, cancellationToken);
            var capacity = _progression.MarchCapacity(available + active.Count);

            if (active.Count >= capacity)
                throw new RequirementNotMetException($"Cannot send more than {capacity} marches at once.");

            // Ціль читається один раз: далі її перевіряють і щит, і підкріплення
            var target = await _targets.ResolveAsync(request.TargetType, request.TargetId, village, cancellationToken);

            // Перевіряємо до зняття юнітів: інакше відмова лишила б гарнізон порожнім
            // Підкріплення може складатись із самого героя, атака не може:
            // порожня армія в бою дала б нульову силу й гарантовану поразку
            if (request.Intent == MarchIntent.Reinforce)
                await _reinforcementRules.EnsureAllowedAsync(
                    village, target, request.Units.Values.Sum(), cancellationToken);
            else if (request.Units.Values.Sum() < 1)
                throw new RequirementNotMetException("An attack needs at least one unit.");
            else
                _targets.EnsureAttackAllowed(village, target);

            // Знімаємо юнітів із гарнізону (перевірки наявності — всередині)
            if (request.Units.Count > 0)
                garrison.SendUnits(request.Units, now);

            hero.Deploy(now);

            var duration = _calculator.CalculateDuration(
                _serverContext.ServerId, village.X, village.Y, target.X, target.Y, request.Units);

            var march = new March(
                Guid.NewGuid(), _serverContext.ServerId, garrison.Id, hero.Id,
                village.X, village.Y, target.X, target.Y,
                request.TargetType, request.TargetId,
                request.Units, now + duration, now,
                request.Intent);

            await _marchRepository.AddAsync(march, cancellationToken);
            await _unitOfWork.SaveChangesAsync(cancellationToken);

            _logger.LogInformation(
                "March {MarchId} sent from ({OriginX},{OriginY}) to ({TargetX},{TargetY}), arrives in {Minutes:F1} min",
                march.Id, village.X, village.Y, target.X, target.Y, duration.TotalMinutes);

            return march.Id;
        }
    }
}
