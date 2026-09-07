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
        MarchIntent Intent = MarchIntent.Attack) : IRequest<Guid>, IPlayerScopedRequest, IIdempotentRequest;

    /// <summary>
    /// Обробник SendMarchCommand: знімає юнітів із гарнізону,
    /// рахує час дороги й ставить похід у дорогу.
    /// </summary>
    public sealed class SendMarchCommandHandler : IRequestHandler<SendMarchCommand, Guid>
    {
        private const int MaxActiveMarches = 3;

        private readonly IVillageRepository _villageRepository;
        private readonly IGarrisonRepository _garrisonRepository;
        private readonly IMarchRepository _marchRepository;
        private readonly IMonsterRepository _monsterRepository;
        private readonly IUnitOfWork _unitOfWork;
        private readonly IServerContext _serverContext;
        private readonly IClanRepository _clanRepository;
        private readonly GameCatalog _catalog;
        private readonly TimeProvider _timeProvider;
        private readonly MarchCalculator _calculator;
        private readonly MarchTargetResolver _targets;
        private readonly ILogger<SendMarchCommandHandler> _logger;

        public SendMarchCommandHandler(
            IVillageRepository villageRepository,
            IGarrisonRepository garrisonRepository,
            IMarchRepository marchRepository,
            IMonsterRepository monsterRepository,
            IClanRepository clanRepository,
            IUnitOfWork unitOfWork,
            IServerContext serverContext,
            GameCatalog catalog,
            TimeProvider timeProvider,
            MarchCalculator calculator,
            MarchTargetResolver targets,
            ILogger<SendMarchCommandHandler> logger)
        {
            _villageRepository = villageRepository;
            _garrisonRepository = garrisonRepository;
            _marchRepository = marchRepository;
            _monsterRepository = monsterRepository;
            _clanRepository = clanRepository;
            _catalog = catalog;
            _serverContext = serverContext;
            _unitOfWork = unitOfWork;
            _calculator = calculator;
            _timeProvider = timeProvider;
            _targets = targets;
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
            var active = await _marchRepository.GetActiveByGarrisonAsync(garrison.Id, cancellationToken);
            if (active.Count >= MaxActiveMarches)
                throw new RequirementNotMetException($"Cannot send more than {MaxActiveMarches} marches at once.");

            // Ціль читається один раз: далі її перевіряють і щит, і підкріплення
            var target = await _targets.ResolveAsync(request.TargetType, request.TargetId, village, cancellationToken);

            // Перевіряємо до зняття юнітів: інакше відмова лишила б гарнізон порожнім
            if (request.Intent == MarchIntent.Reinforce)
                await EnsureCanReinforceAsync(request, village, target, cancellationToken);
            else
                _targets.EnsureAttackAllowed(village, target);

            // Знімаємо юнітів із гарнізону (перевірки наявності — всередині)
            garrison.SendUnits(request.Units, now);

            var duration = _calculator.CalculateDuration(
                _serverContext.ServerId, village.X, village.Y, target.X, target.Y, request.Units);

            var march = new March(
                Guid.NewGuid(), _serverContext.ServerId, garrison.Id,
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

        /// <summary>
        /// Підкріплення йдуть лише до союзника і лише якщо в посольстві є місце.
        /// Обидві умови перевіряються ще раз на прибутті: дорога довга.
        /// </summary>
        private async Task EnsureCanReinforceAsync(SendMarchCommand request, Village origin, MarchTarget target,
            CancellationToken cancellationToken)
        {
            if (target.Village is not { } destination)
                throw new RequirementNotMetException(
                    $"Reinforcement march must target a village, got '{request.TargetType}'.");

            if (destination.PlayerId == request.PlayerId)
                throw new RequirementNotMetException("You cannot reinforce your own village.");

            // Підкріплення відкриваються з того самого порогу, що знімає щит:
            // недоторканне село інакше стало б сейфом для кланової армії
            var shieldLevel = _catalog.Config.Combat.NewbieShieldTownHallLevel;

            if (origin.IsShielded(_catalog.Buildings, shieldLevel))
                throw new RequirementNotMetException(
                    $"Reinforcements are available from town hall level {shieldLevel}.");

            if (destination.IsShielded(_catalog.Buildings, shieldLevel))
                throw new RequirementNotMetException("This village cannot receive reinforcements yet.");

            var myClan = await _clanRepository.GetClanIdByMemberAsync(request.PlayerId, cancellationToken);
            var targetClan = await _clanRepository.GetClanIdByMemberAsync(destination.PlayerId, cancellationToken);

            if (myClan is null || myClan != targetClan)
                throw new RequirementNotMetException("Reinforcements go to clanmates only.");

            var targetGarrison = await _garrisonRepository.GetByVillageIdAsync(destination.Id, cancellationToken)
                ?? throw new InvalidOperationException($"Garrison not found for village {destination.Id}.");

            var free = destination.ReinforcementCapacity(_catalog.Buildings) - targetGarrison.ReinforcementCount;
            var incoming = request.Units.Values.Sum();

            if (incoming > free)
                throw new RequirementNotMetException(
                    $"The embassy has room for {free} more units, you are sending {incoming}.");
        }
    }
}
