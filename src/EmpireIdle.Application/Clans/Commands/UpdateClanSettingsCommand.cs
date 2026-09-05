using EmpireIdle.Application.Common.Security;
using EmpireIdle.Application.Interfaces;
using EmpireIdle.Domain.Enums;
using EmpireIdle.Domain.Exceptions;
using MediatR;
using Microsoft.Extensions.Logging;

namespace EmpireIdle.Application.Clans.Commands
{
    /// <summary>
    /// Опис і політика вступу. Без неї новий клан назавжди лишається
    /// ByApproval — саме таким він виходить із конструктора.
    /// </summary>
    public record UpdateClanSettingsCommand(Guid PlayerId, string Description, ClanJoinPolicy JoinPolicy)
        : IRequest, IPlayerScopedRequest, IIdempotentRequest;

    /// <summary>Обробник UpdateClanSettingsCommand.</summary>
    public sealed class UpdateClanSettingsCommandHandler : IRequestHandler<UpdateClanSettingsCommand>
    {
        private readonly IClanRepository _clanRepository;
        private readonly IUnitOfWork _unitOfWork;
        private readonly TimeProvider _timeProvider;
        private readonly ILogger<UpdateClanSettingsCommandHandler> _logger;

        public UpdateClanSettingsCommandHandler(
            IClanRepository clanRepository,
            IUnitOfWork unitOfWork,
            TimeProvider timeProvider,
            ILogger<UpdateClanSettingsCommandHandler> logger)
        {
            _clanRepository = clanRepository;
            _unitOfWork = unitOfWork;
            _timeProvider = timeProvider;
            _logger = logger;
        }

        public async Task Handle(UpdateClanSettingsCommand request, CancellationToken cancellationToken)
        {
            var now = _timeProvider.GetUtcNow().UtcDateTime;

            var clan = await _clanRepository.GetByMemberAsync(request.PlayerId, cancellationToken)
                ?? throw new InvalidStateException("You are not in a clan.");

            // Дозвіл EditProfile перевіряє агрегат
            clan.UpdateSettings(request.PlayerId, request.Description, request.JoinPolicy, now);

            await _unitOfWork.SaveChangesAsync(cancellationToken);

            _logger.LogInformation("Clan {ClanId} settings updated by {PlayerId}, join policy {JoinPolicy}",
                clan.Id, request.PlayerId, request.JoinPolicy);
        }
    }
}
