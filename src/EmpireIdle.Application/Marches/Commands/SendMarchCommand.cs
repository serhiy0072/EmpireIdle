using EmpireIdle.Application.Beasts.Services;
using EmpireIdle.Application.Common.Security;
using EmpireIdle.Application.Common.Services;
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
        IReadOnlyList<Guid> HeroIds,
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
        private readonly HeroConvoys _convoys;
        private readonly GameCatalog _catalog;
        private readonly BeastTamer _tamer;
        private readonly EffectResolver _effects;
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
            HeroConvoys convoys,
            GameCatalog catalog,
            BeastTamer tamer,
            EffectResolver effects,
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
            _convoys = convoys;
            _catalog = catalog;
            _tamer = tamer;
            _effects = effects;
            _logger = logger;
        }

        public async Task<Guid> Handle(SendMarchCommand request, CancellationToken cancellationToken)
        {
            var now = _timeProvider.GetUtcNow().UtcDateTime;

            var village = await _villageRepository.GetByPlayerIdAsync(request.PlayerId, cancellationToken)
                ?? throw new InvalidOperationException($"Village not found for player {request.PlayerId}.");

            var garrison = await _garrisonRepository.GetByVillageIdAsync(village.Id, cancellationToken)
                ?? throw new InvalidOperationException($"Garrison not found for village {village.Id}.");

            var heroIds = request.HeroIds.Distinct().ToList();
            var heroes = await _heroRepository.GetByIdsAsync(heroIds, cancellationToken);

            // Чужий герой не відрізняється від неіснуючого: інакше відповідь
            // підтверджувала б, що такий герой у когось є
            var missing = heroIds.FirstOrDefault(id => heroes.All(h => h.Id != id || h.PlayerId != request.PlayerId));

            if (missing != Guid.Empty)
                throw new EntityNotFoundException("Hero", missing.ToString());

            foreach (var hero in heroes)
            {
                if (!hero.IsAvailable)
                    throw new RequirementNotMetException(RefusalReasons.MarchHeroUnavailable,
                        $"Hero {hero.Id} is {hero.State}.", hero.State.ToString());

                // Герой веде похід звідти, де стоїть. Без цієї перевірки герой
                // із гарнізону союзника телепортувався б додому, лишивши там
                // свій стек без лідера
                if (hero.StationedGarrisonId != garrison.Id)
                    throw new RequirementNotMetException(RefusalReasons.MarchHeroElsewhere,
                        $"Hero {hero.Id} is stationed in another garrison.");
            }

            var leaders = heroes.Select(h => (Hero: h, Config: _catalog.FindHero(h.HeroKey))).ToList();

            var active = await _marchRepository.GetActiveByGarrisonAsync(garrison.Id, cancellationToken);

            // Похід без вільного героя не вийде (перевірка вище), тож вільних тут щонайменше один
            // і герої межу не звужують — фактично стримує стеля MaxMarches. Активні марші рахуються
            // всі, з розвідкою включно, а героїв у марші буває до трьох (GDD §6.1)
            var availableHeroes = await _heroRepository.CountAvailableAsync(
                request.PlayerId, garrison.Id, cancellationToken);

            var capacity = _progression.MarchCapacity(availableHeroes + active.Count);

            if (active.Count >= capacity)
                throw new RequirementNotMetException(RefusalReasons.MarchCapacity,
                    $"Cannot send more than {capacity} marches at once.", capacity);

            // До трьох героїв різних ролей; кожен веде лише юнітів своєї ролі й не більше за свої конвої (GDD §6.1)
            _convoys.EnsureCanLead(leaders, request.Units);

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
                await _targets.EnsureAttackAllowedAsync(village, target, now, cancellationToken);

                if (request.Intent == MarchIntent.Tame)
                    await _tamer.EnsureAllowedAsync(village, request.TargetId, cancellationToken);

                // Напад на гравця — на село чи на його табір — знімає власний щит
                // після падіння: інакше з-під нього можна було б безкарно атакувати
                if (target.Village is not null || target.Camp is not null)
                    village.DropShield(now);
            }

            // Знімаємо юнітів із гарнізону (перевірки наявності — всередині). Похід із самого
            // героя теж зрушує гарнізон: на його xmin тримається стеля маршів
            if (request.Units.Count > 0)
                garrison.SendUnits(request.Units, now);
            else
                garrison.SendHeroAlone(now);

            var marchId = Guid.NewGuid();

            foreach (var hero in heroes)
                hero.Deploy(marchId, now);

            // Колона йде за найповільнішим учасником, і герої тут нарівні
            // з юнітами: підкріплення з самих героїв інакше плелося б
            // базовою швидкістю замість власної.
            // Пасивка звіра й небойові вміння героїв пришвидшують марш; множник фіксується
            // на марші й діє на зворотній дорозі
            var speed = await _effects.GetMultiplierAsync(request.PlayerId, EffectTarget.MarchSpeed, now, cancellationToken)
                * _progression.MarchSpeedMultiplier(leaders);

            var duration = _calculator.CalculateDuration(
                _serverContext.ServerId, village.X, village.Y, target.X, target.Y, request.Units,
                _progression.MarchSpeed(leaders.Select(l => l.Config))) / speed;

            var march = new March(
                marchId, _serverContext.ServerId, garrison.Id,
                village.X, village.Y, target.X, target.Y,
                request.TargetType, request.TargetId,
                request.Units, now + duration, now,
                request.Intent, speed);

            await _marchRepository.AddAsync(march, cancellationToken);
            await _unitOfWork.SaveChangesAsync(cancellationToken);

            _logger.LogInformation(
                "March {MarchId} sent from ({OriginX},{OriginY}) to ({TargetX},{TargetY}) led by {Heroes} heroes, "
                + "arrives in {Minutes:F1} min",
                march.Id, village.X, village.Y, target.X, target.Y, heroes.Count, duration.TotalMinutes);

            return march.Id;
        }
    }
}
