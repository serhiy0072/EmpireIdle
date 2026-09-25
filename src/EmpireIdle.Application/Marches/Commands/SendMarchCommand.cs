using EmpireIdle.Application.Common.Security;
using EmpireIdle.Application.Interfaces;
using EmpireIdle.Application.Marches.Services;
using EmpireIdle.Application.Territory.Services;
using EmpireIdle.Domain.Entities;
using EmpireIdle.Domain.Enums;
using EmpireIdle.Domain.Exceptions;
using EmpireIdle.Domain.Services;
using EmpireIdle.Domain.ValueObjects;
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
        Dictionary<UnitStackKey, int> Units,
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
        private readonly StructureMarchRules _structureMarches;
        private readonly HeroProgression _progression;
        private readonly GameCatalog _catalog;
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
            StructureMarchRules structureMarches,
            HeroProgression progression,
            GameCatalog catalog,
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
            _structureMarches = structureMarches;
            _progression = progression;
            _catalog = catalog;
            _logger = logger;
        }

        public async Task<Guid> Handle(SendMarchCommand request, CancellationToken cancellationToken)
        {
            var now = _timeProvider.GetUtcNow().UtcDateTime;

            var village = await _villageRepository.GetByPlayerIdAsync(request.PlayerId, cancellationToken)
                ?? throw new InvalidOperationException($"Village not found for player {request.PlayerId}.");

            var garrison = await _garrisonRepository.GetByVillageIdAsync(village.Id, cancellationToken)
                ?? throw new InvalidOperationException($"Garrison not found for village {village.Id}.");

            var hero = await _heroRepository.GetByIdAsync(request.HeroId, cancellationToken)
                ?? throw new EntityNotFoundException("Hero", request.HeroId.ToString());

            // Чужий герой не відрізняється від неіснуючого: інакше відповідь
            // підтверджувала б, що такий герой у когось є
            if (hero.PlayerId != request.PlayerId)
                throw new EntityNotFoundException("Hero", request.HeroId.ToString());

            if (!hero.IsAvailable)
                throw new RequirementNotMetException(RefusalReasons.MarchHeroUnavailable,
                    $"Hero {request.HeroId} is {hero.State}.", hero.State.ToString());

            // Герой веде похід звідти, де стоїть. Без цієї перевірки герой
            // із гарнізону союзника телепортувався б додому, лишивши там
            // свій стек без лідера
            if (hero.StationedGarrisonId != garrison.Id)
                throw new RequirementNotMetException(RefusalReasons.MarchHeroElsewhere,
                    $"Hero {request.HeroId} is stationed in another garrison.");

            var active = await _marchRepository.GetActiveByGarrisonAsync(garrison.Id, cancellationToken);

            // Ростер це вільні герої плюс ті, хто вже в дорозі: кожен похід
            // веде свій герой, тож вільний герой і є вільним слотом, а
            // MaxMarches лишається стелею згори
            var availableHeroes = await _heroRepository.CountAvailableAsync(
                request.PlayerId, garrison.Id, cancellationToken);

            var capacity = _progression.MarchCapacity(availableHeroes + active.Count);

            if (active.Count >= capacity)
                throw new RequirementNotMetException(RefusalReasons.MarchCapacity,
                    $"Cannot send more than {capacity} marches at once.", capacity);

            // Ціль читається один раз: далі її перевіряють і щит, і підкріплення
            var target = await _targets.ResolveAsync(request.TargetType, request.TargetId, village, now, cancellationToken);

            // Перевіряємо до зняття юнітів: інакше відмова лишила б гарнізон порожнім.
            // Підкріплення може складатись із самого героя, атака не може:
            // порожня армія в бою дала б нульову силу й гарантовану поразку
            if (target.Structure is not null)
                await _structureMarches.EnsureAllowedAsync(request.PlayerId, target.Structure, request.Intent, cancellationToken);

            // Гарнізон споруди місткість звіряє на прибутті: будівництво прискорює й повний
            if (request.Intent == MarchIntent.Reinforce)
            {
                if (target.Structure is null)
                    await _reinforcementRules.EnsureAllowedAsync(
                        village, target, request.Units.Values.Sum(), cancellationToken);
            }
            else if (request.Units.Values.Sum() < 1)
                throw new RequirementNotMetException(RefusalReasons.MarchEmptyAttack, "An attack needs at least one unit.");
            else
            {
                _targets.EnsureAttackAllowed(village, target, now);

                // Напад на гравця знімає власний щит після падіння — інакше
                // з-під нього можна було б безкарно атакувати
                if (target.Village is not null)
                    village.DropShield(now);
            }

            // Знімаємо юнітів із гарнізону (перевірки наявності — всередині)
            if (request.Units.Count > 0)
                garrison.SendUnits(request.Units, now);

            hero.Deploy(now);

            // Колона йде за найповільнішим учасником, і герой тут нарівні
            // з юнітами: підкріплення з самого героя інакше плелося б
            // базовою швидкістю замість власної
            var duration = _calculator.CalculateDuration(
                _serverContext.ServerId, village.X, village.Y, target.X, target.Y, request.Units,
                _progression.MarchSpeed(_catalog.FindHero(hero.HeroKey)));

            var march = new March(
                Guid.NewGuid(), _serverContext.ServerId, garrison.Id, hero.Id,
                village.X, village.Y, target.X, target.Y,
                request.TargetType, request.TargetId,
                request.Units, now + duration, now,
                request.Intent);

            await _marchRepository.AddAsync(march, cancellationToken);
            await _unitOfWork.SaveChangesAsync(cancellationToken);

            _logger.LogInformation(
                "March {MarchId} sent from ({OriginX},{OriginY}) to ({TargetX},{TargetY}) led by hero {HeroId}, "
                + "arrives in {Minutes:F1} min",
                march.Id, village.X, village.Y, target.X, target.Y, hero.Id, duration.TotalMinutes);

            return march.Id;
        }
    }
}
