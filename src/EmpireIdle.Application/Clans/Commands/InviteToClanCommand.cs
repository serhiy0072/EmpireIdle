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
    /// Запрошення гравця в клан.
    ///
    /// PlayerId — виконавець, ціль окремим полем: PlayerScopeBehavior
    /// звіряє перше з токеном, і без другого запросити можна було б лише себе.
    /// </summary>
    public record InviteToClanCommand(Guid PlayerId, Guid TargetPlayerId)
        : IRequest<Guid>, IPlayerScopedRequest, IIdempotentRequest;

    /// <summary>Обробник InviteToClanCommand.</summary>
    public sealed class InviteToClanCommandHandler : IRequestHandler<InviteToClanCommand, Guid>
    {
        private readonly IClanRepository _clanRepository;
        private readonly IClanRequestRepository _requestRepository;
        private readonly IPlayerRepository _playerRepository;
        private readonly IUnitOfWork _unitOfWork;
        private readonly GameCatalog _catalog;
        private readonly TimeProvider _timeProvider;
        private readonly ILogger<InviteToClanCommandHandler> _logger;

        public InviteToClanCommandHandler(
            IClanRepository clanRepository,
            IClanRequestRepository requestRepository,
            IPlayerRepository playerRepository,
            IUnitOfWork unitOfWork,
            GameCatalog catalog,
            TimeProvider timeProvider,
            ILogger<InviteToClanCommandHandler> logger)
        {
            _clanRepository = clanRepository;
            _requestRepository = requestRepository;
            _playerRepository = playerRepository;
            _unitOfWork = unitOfWork;
            _catalog = catalog;
            _timeProvider = timeProvider;
            _logger = logger;
        }

        public async Task<Guid> Handle(InviteToClanCommand request, CancellationToken cancellationToken)
        {
            var now = _timeProvider.GetUtcNow().UtcDateTime;

            var clan = await _clanRepository.GetByMemberAsync(request.PlayerId, cancellationToken)
                ?? throw new InvalidStateException("You are not in a clan.");

            clan.EnsureCan(request.PlayerId, ClanPermission.Recruit);

            var target = await _playerRepository.GetByIdAsync(request.TargetPlayerId, cancellationToken)
                ?? throw new EntityNotFoundException("Player", request.TargetPlayerId);

            if (target.ClanId is not null)
                throw new RequirementNotMetException("This player is already in a clan.");

            var previous = await _requestRepository.GetLatestAsync(
                clan.Id, target.Id, ClanRequestKind.Invite, cancellationToken);

            if (previous is not null && previous.IsPending(now))
                throw new AlreadyExistsException("Clan invite", target.Id.ToString());

            // Кулдауну на запрошення немає: відмова гравця не має карати клан,
            // а спам обмежує строк життя й одна відкрита пропозиція на пару
            var invite = new ClanRequest(
                Guid.NewGuid(), clan.ServerId, clan.Id, target.Id, ClanRequestKind.Invite,
                now.AddHours(_catalog.Config.Clan.RequestLifetimeHours), now);

            await _requestRepository.AddAsync(invite, cancellationToken);

            await _unitOfWork.SaveChangesAsync(cancellationToken);

            _logger.LogInformation("Player {TargetId} invited to clan {ClanId} by {ActorId}",
                target.Id, clan.Id, request.PlayerId);

            return invite.Id;
        }
    }
}
