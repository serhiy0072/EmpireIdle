using EmpireIdle.Application.Common.Security;
using EmpireIdle.Application.Interfaces;
using EmpireIdle.Domain.Exceptions;
using MediatR;
using Microsoft.Extensions.Logging;

namespace EmpireIdle.Application.Clans.Commands
{
    /// <summary>Призначає учаснику роль. Права й ранги перевіряє агрегат.</summary>
    public record AssignRoleCommand(Guid PlayerId, Guid TargetPlayerId, Guid RoleId)
        : IRequest, IPlayerScopedRequest, IIdempotentRequest;

    public sealed class AssignRoleCommandHandler : IRequestHandler<AssignRoleCommand>
    {
        private readonly IClanRepository _clanRepository;
        private readonly IUnitOfWork _unitOfWork;
        private readonly TimeProvider _timeProvider;
        private readonly ILogger<AssignRoleCommandHandler> _logger;

        public AssignRoleCommandHandler(
            IClanRepository clanRepository,
            IUnitOfWork unitOfWork,
            TimeProvider timeProvider,
            ILogger<AssignRoleCommandHandler> logger)
        {
            _clanRepository = clanRepository;
            _unitOfWork = unitOfWork;
            _timeProvider = timeProvider;
            _logger = logger;
        }

        public async Task Handle(AssignRoleCommand request, CancellationToken cancellationToken)
        {
            var now = _timeProvider.GetUtcNow().UtcDateTime;

            var clan = await _clanRepository.GetByMemberAsync(request.PlayerId, cancellationToken)
                ?? throw new InvalidStateException("You are not in a clan.");

            clan.AssignRole(request.PlayerId, request.TargetPlayerId, request.RoleId, now);

            await _unitOfWork.SaveChangesAsync(cancellationToken);

            _logger.LogInformation("Player {TargetId} assigned role {RoleId} in clan {ClanId} by {ActorId}",
                request.TargetPlayerId, request.RoleId, clan.Id, request.PlayerId);
        }
    }
}
