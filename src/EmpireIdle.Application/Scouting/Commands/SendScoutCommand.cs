using EmpireIdle.Application.Common.Security;
using EmpireIdle.Application.Interfaces;
using EmpireIdle.Application.Marches.Services;
using EmpireIdle.Application.Scouting.Services;
using EmpireIdle.Domain.Entities;
using EmpireIdle.Domain.Enums;
using EmpireIdle.Domain.Exceptions;
using EmpireIdle.Domain.Services;
using EmpireIdle.Domain.ValueObjects;
using MediatR;
using Microsoft.Extensions.Logging;

namespace EmpireIdle.Application.Scouting.Commands
{
    /// <summary>
    /// Відправити розвідників на чуже село чи споруду клану. Досить мати вежу розвідки:
    /// ні героя, ні юнітів, ні ліміту на кількість і дальність. Ціль бачить марш і ім'я.
    /// </summary>
    public record SendScoutCommand(Guid PlayerId, MarchTargetType TargetType, Guid TargetId)
        : IRequest<Guid>, IPlayerScopedRequest, IIdempotentRequest;

    public sealed class SendScoutCommandHandler : IRequestHandler<SendScoutCommand, Guid>
    {
        private static readonly IReadOnlyDictionary<UnitStackKey, int> NoUnits = new Dictionary<UnitStackKey, int>();

        private readonly IVillageRepository _villageRepository;
        private readonly IGarrisonRepository _garrisonRepository;
        private readonly IMarchRepository _marchRepository;
        private readonly IClanRepository _clanRepository;
        private readonly IUnitOfWork _unitOfWork;
        private readonly IServerContext _serverContext;
        private readonly TimeProvider _timeProvider;
        private readonly MarchCalculator _calculator;
        private readonly MarchTargetResolver _targets;
        private readonly ScoutVisibility _visibility;
        private readonly VillageStatus _status;
        private readonly GameCatalog _catalog;
        private readonly ILogger<SendScoutCommandHandler> _logger;

        public SendScoutCommandHandler(
            IVillageRepository villageRepository,
            IGarrisonRepository garrisonRepository,
            IMarchRepository marchRepository,
            IClanRepository clanRepository,
            IUnitOfWork unitOfWork,
            IServerContext serverContext,
            TimeProvider timeProvider,
            MarchCalculator calculator,
            MarchTargetResolver targets,
            ScoutVisibility visibility,
            VillageStatus status,
            GameCatalog catalog,
            ILogger<SendScoutCommandHandler> logger)
        {
            _villageRepository = villageRepository;
            _garrisonRepository = garrisonRepository;
            _marchRepository = marchRepository;
            _clanRepository = clanRepository;
            _unitOfWork = unitOfWork;
            _serverContext = serverContext;
            _timeProvider = timeProvider;
            _calculator = calculator;
            _targets = targets;
            _visibility = visibility;
            _status = status;
            _catalog = catalog;
            _logger = logger;
        }

        public async Task<Guid> Handle(SendScoutCommand request, CancellationToken cancellationToken)
        {
            var now = _timeProvider.GetUtcNow().UtcDateTime;
            var scouting = _catalog.Config.Combat.Scouting;

            var tower = scouting.RequiredBuilding
                ?? throw new RequirementNotMetException("Scouting is disabled in this world.");

            var village = await _villageRepository.GetByPlayerIdAsync(request.PlayerId, cancellationToken)
                ?? throw new InvalidOperationException($"Village not found for player {request.PlayerId}.");

            // Вежа стоїть із дня 1 під туманом — рахується лише відкрита
            if (village.Buildings.All(b => b.Type != tower) || !_status.IsUnlocked(village, tower))
                throw new RequirementNotMetException(RefusalReasons.BuildingRequired,
                    $"Scouting requires a '{tower}'.", _catalog.Building(tower).DisplayName);

            var garrison = await _garrisonRepository.GetByVillageIdAsync(village.Id, cancellationToken)
                ?? throw new InvalidOperationException($"Garrison not found for village {village.Id}.");

            var target = await _targets.ResolveAsync(request.TargetType, request.TargetId, village, now, cancellationToken);

            if (target.Village?.PlayerId == request.PlayerId
                || (target.Structure is { } structure
                    && await _clanRepository.GetClanIdByMemberAsync(request.PlayerId, cancellationToken) == structure.ClanId))
                throw new RequirementNotMetException(RefusalReasons.ScoutOwnTarget, "You cannot scout your own village or clan.");

            // Завіса діє вже зараз — розвідники нічого не побачать, тож і не йдуть
            if (await _visibility.IsHiddenAsync(target, now, cancellationToken))
                throw new InvalidStateException(RefusalReasons.ScoutBlocked, "The target is hidden from scouting.");

            // Юнітів у розвідників немає — колона йде їхньою власною швидкістю
            var duration = _calculator.CalculateDuration(
                _serverContext.ServerId, village.X, village.Y, target.X, target.Y, scouting.Speed);

            var march = new March(
                Guid.NewGuid(), _serverContext.ServerId, garrison.Id, heroId: null,
                village.X, village.Y, target.X, target.Y,
                request.TargetType, request.TargetId,
                NoUnits, now + duration, now,
                MarchIntent.Scout);

            await _marchRepository.AddAsync(march, cancellationToken);
            await _unitOfWork.SaveChangesAsync(cancellationToken);

            _logger.LogInformation("Scouts {MarchId} sent from ({OriginX},{OriginY}) to ({TargetX},{TargetY}), arrive in {Minutes:F1} min",
                march.Id, village.X, village.Y, target.X, target.Y, duration.TotalMinutes);

            return march.Id;
        }
    }
}
