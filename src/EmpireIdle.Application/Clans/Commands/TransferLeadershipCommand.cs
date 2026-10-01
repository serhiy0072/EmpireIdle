using EmpireIdle.Application.Common.Security;
using EmpireIdle.Application.Interfaces;
using EmpireIdle.Domain.Exceptions;
using MediatR;
using Microsoft.Extensions.Logging;

namespace EmpireIdle.Application.Clans.Commands
{
    /// <summary>
    /// Лідер добровільно передає лідерство іншому учаснику; сам стає на другу за рангом роль.
    /// Хто має право — перевіряє агрегат. PlayerId — виконавець, ціль — окремим полем.
    /// </summary>
    public record TransferLeadershipCommand(Guid PlayerId, Guid TargetPlayerId)
        : IRequest, IPlayerScopedRequest, IIdempotentRequest;

    public sealed class TransferLeadershipCommandHandler : IRequestHandler<TransferLeadershipCommand>
    {
        private readonly IClanRepository _clanRepository;
        private readonly IUnitOfWork _unitOfWork;
        private readonly TimeProvider _timeProvider;
        private readonly ILogger<TransferLeadershipCommandHandler> _logger;

        public TransferLeadershipCommandHandler(
            IClanRepository clanRepository,
            IUnitOfWork unitOfWork,
            TimeProvider timeProvider,
            ILogger<TransferLeadershipCommandHandler> logger)
        {
            _clanRepository = clanRepository;
            _unitOfWork = unitOfWork;
            _timeProvider = timeProvider;
            _logger = logger;
        }

        public async Task Handle(TransferLeadershipCommand request, CancellationToken cancellationToken)
        {
            var now = _timeProvider.GetUtcNow().UtcDateTime;

            if (request.PlayerId == request.TargetPlayerId)
                throw new RequirementNotMetException("You already lead the clan.");

            var clan = await _clanRepository.GetByMemberAsync(request.PlayerId, cancellationToken)
                ?? throw new InvalidStateException(RefusalReasons.ClanNotMember, "You are not in a clan.");

            clan.TransferLeadership(request.PlayerId, request.TargetPlayerId, now);

            await _unitOfWork.SaveChangesAsync(cancellationToken);

            _logger.LogInformation("Clan {ClanId}: leadership passed from {ActorId} to {TargetId}",
                clan.Id, request.PlayerId, request.TargetPlayerId);
        }
    }
}
