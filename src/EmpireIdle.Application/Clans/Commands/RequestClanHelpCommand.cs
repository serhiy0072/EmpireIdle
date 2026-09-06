using EmpireIdle.Application.Common.Security;
using EmpireIdle.Application.Interfaces;
using EmpireIdle.Domain.Entities;
using EmpireIdle.Domain.Enums;
using EmpireIdle.Domain.Exceptions;
using EmpireIdle.Domain.Services;
using MediatR;
using Microsoft.Extensions.Logging;

namespace EmpireIdle.Application.Clans.Commands
{
    /// <summary>
    /// Просить клан прискорити таймер. Створює гравець вручну:
    /// автоматичний запит на кожен апгрейд засмітив би список клану.
    /// </summary>
    public record RequestClanHelpCommand(Guid PlayerId, ClanHelpTarget TargetType, Guid TargetId)
        : IRequest<Guid>, IPlayerScopedRequest, IIdempotentRequest;

    public sealed class RequestClanHelpCommandHandler : IRequestHandler<RequestClanHelpCommand, Guid>
    {
        private readonly IClanRepository _clanRepository;
        private readonly IClanHelpRepository _helpRepository;
        private readonly IVillageRepository _villageRepository;
        private readonly IGarrisonRepository _garrisonRepository;
        private readonly IUnitOfWork _unitOfWork;
        private readonly IServerContext _serverContext;
        private readonly GameCatalog _catalog;
        private readonly TimeProvider _timeProvider;
        private readonly ILogger<RequestClanHelpCommandHandler> _logger;

        public RequestClanHelpCommandHandler(
            IClanRepository clanRepository,
            IClanHelpRepository helpRepository,
            IVillageRepository villageRepository,
            IGarrisonRepository garrisonRepository,
            IUnitOfWork unitOfWork,
            IServerContext serverContext,
            GameCatalog catalog,
            TimeProvider timeProvider,
            ILogger<RequestClanHelpCommandHandler> logger)
        {
            _clanRepository = clanRepository;
            _helpRepository = helpRepository;
            _villageRepository = villageRepository;
            _garrisonRepository = garrisonRepository;
            _unitOfWork = unitOfWork;
            _serverContext = serverContext;
            _catalog = catalog;
            _timeProvider = timeProvider;
            _logger = logger;
        }

        public async Task<Guid> Handle(RequestClanHelpCommand request, CancellationToken cancellationToken)
        {
            var now = _timeProvider.GetUtcNow().UtcDateTime;

            var clan = await _clanRepository.GetByMemberAsync(request.PlayerId, cancellationToken)
                ?? throw new InvalidStateException("You are not in a clan.");

            if (await _helpRepository.ExistsForTargetAsync(request.TargetId, cancellationToken))
                throw new AlreadyExistsException("Help request", request.TargetId.ToString());

            var (fullDuration, completesAt) = await ResolveTimerAsync(request, now, cancellationToken);

            var clanConfig = _catalog.Config.Clan;

            // Запит живе менше за таймер: коли будівництво завершиться,
            // допомагати вже нема чому
            var expiresAt = completesAt < now.AddHours(clanConfig.HelpRequestLifetimeHours)
                ? completesAt
                : now.AddHours(clanConfig.HelpRequestLifetimeHours);

            var helpRequest = new ClanHelpRequest(
                Guid.NewGuid(), _serverContext.ServerId, clan.Id, request.PlayerId,
                request.TargetType, request.TargetId, fullDuration, expiresAt, now);

            await _helpRepository.AddAsync(helpRequest, cancellationToken);
            clan.RecordActivity(request.PlayerId, now);

            await _unitOfWork.SaveChangesAsync(cancellationToken);

            _logger.LogInformation("Player {PlayerId} requested clan help for {TargetType} {TargetId}",
                request.PlayerId, request.TargetType, request.TargetId);

            return helpRequest.Id;
        }

        /// <summary>
        /// Повна тривалість таймера й момент завершення.
        ///
        /// Тривалість рахується з конфіга, а не як «залишок»: частка має
        /// братись від повного часу, інакше двадцять допомог не складуть
        /// обіцяних сорока відсотків.
        /// </summary>
        private async Task<(TimeSpan FullDuration, DateTime CompletesAt)> ResolveTimerAsync(
            RequestClanHelpCommand request, DateTime now, CancellationToken cancellationToken)
        {
            var village = await _villageRepository.GetByPlayerIdAsync(request.PlayerId, cancellationToken)
                ?? throw new EntityNotFoundException("Village for player", request.PlayerId);

            switch (request.TargetType)
            {
                case ClanHelpTarget.Construction:
                    var building = village.Buildings.FirstOrDefault(b => b.Id == request.TargetId)
                        ?? throw new EntityNotFoundException("Building", request.TargetId);

                    if (!building.IsUnderConstruction)
                        throw new InvalidStateException("That building is not under construction.");

                    var config = _catalog.Building(building.Type);

                    // Рівень уже піднімуть при завершенні, тому крива береться
                    // від поточного — того самого, що й при старті апгрейду
                    var minutes = config.BaseBuildMinutes
                                  * Math.Pow(config.BuildTimeGrowth, building.Level.Value - 1);

                    return (TimeSpan.FromMinutes(minutes), building.ConstructionCompletesAt!.Value);

                case ClanHelpTarget.Training:
                    var garrison = await _garrisonRepository.GetByVillageIdAsync(village.Id, cancellationToken)
                        ?? throw new EntityNotFoundException("Garrison for village", village.Id);

                    var order = garrison.TrainingOrders.FirstOrDefault(o => o.Id == request.TargetId)
                        ?? throw new EntityNotFoundException("Training order", request.TargetId);

                    var unit = _catalog.Unit(order.UnitType);

                    return (TimeSpan.FromMinutes(unit.BaseTrainMinutes * order.Count), order.CompletesAt);

                default:
                    throw new RequirementNotMetException($"Unsupported help target '{request.TargetType}'.");
            }
        }
    }
}
