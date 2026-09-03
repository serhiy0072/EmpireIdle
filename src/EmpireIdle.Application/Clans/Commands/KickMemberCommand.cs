using EmpireIdle.Application.Common.Security;
using EmpireIdle.Application.Interfaces;
using EmpireIdle.Domain.Exceptions;
using MediatR;
using Microsoft.Extensions.Logging;

namespace EmpireIdle.Application.Clans.Commands
{
    /// <summary>
    /// Виключення з клану.
    ///
    /// PlayerId — виконавець, не ціль: PlayerScopeBehavior звіряє його
    /// з токеном, і ціль мусить іти окремим полем, інакше кікнути можна
    /// було б лише себе.
    /// </summary>
    public record KickMemberCommand(Guid PlayerId, Guid TargetPlayerId)
        : IRequest, IPlayerScopedRequest, IIdempotentRequest;

    public sealed class KickMemberCommandHandler : IRequestHandler<KickMemberCommand>
    {
        private readonly IClanRepository _clanRepository;
        private readonly IPlayerRepository _playerRepository;
        private readonly IUnitOfWork _unitOfWork;
        private readonly TimeProvider _timeProvider;
        private readonly ILogger<KickMemberCommandHandler> _logger;

        public KickMemberCommandHandler(
            IClanRepository clanRepository,
            IPlayerRepository playerRepository,
            IUnitOfWork unitOfWork,
            TimeProvider timeProvider,
            ILogger<KickMemberCommandHandler> logger)
        {
            _clanRepository = clanRepository;
            _playerRepository = playerRepository;
            _unitOfWork = unitOfWork;
            _timeProvider = timeProvider;
            _logger = logger;
        }

        public async Task Handle(KickMemberCommand request, CancellationToken cancellationToken)
        {
            var now = _timeProvider.GetUtcNow().UtcDateTime;

            if (request.PlayerId == request.TargetPlayerId)
                throw new RequirementNotMetException("Use leave instead of kicking yourself.");

            var clan = await _clanRepository.GetByMemberAsync(request.PlayerId, cancellationToken)
                ?? throw new InvalidStateException("You are not in a clan.");

            var target = await _playerRepository.GetByIdAsync(request.TargetPlayerId, cancellationToken)
                ?? throw new EntityNotFoundException("Player", request.TargetPlayerId);

            // Право й ранг перевіряє агрегат: він знає ролі, а хендлер ні
            clan.Kick(request.PlayerId, target.Id, now);
            target.LeaveClan();

            await _unitOfWork.SaveChangesAsync(cancellationToken);

            _logger.LogInformation("Player {TargetId} kicked from clan {ClanId} by {ActorId}",
                target.Id, clan.Id, request.PlayerId);
        }
    }
}
