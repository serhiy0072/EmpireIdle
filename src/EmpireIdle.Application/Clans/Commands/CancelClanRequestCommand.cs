using EmpireIdle.Application.Common.Security;
using EmpireIdle.Application.Interfaces;
using EmpireIdle.Domain.Enums;
using EmpireIdle.Domain.Exceptions;
using MediatR;
using Microsoft.Extensions.Logging;

namespace EmpireIdle.Application.Clans.Commands
{
    /// <summary>
    /// Знімає власну заявку або відкликає надіслане клубом запрошення.
    /// Дзеркало ResolveClanRequestCommand: там вирішує адресат, тут ініціатор.
    /// </summary>
    public record CancelClanRequestCommand(Guid PlayerId, Guid RequestId)
        : IRequest, IPlayerScopedRequest, IIdempotentRequest;

    /// <summary>Обробник CancelClanRequestCommand.</summary>
    public sealed class CancelClanRequestCommandHandler : IRequestHandler<CancelClanRequestCommand>
    {
        private readonly IClanRepository _clanRepository;
        private readonly IClanRequestRepository _requestRepository;
        private readonly IUnitOfWork _unitOfWork;
        private readonly TimeProvider _timeProvider;
        private readonly ILogger<CancelClanRequestCommandHandler> _logger;

        public CancelClanRequestCommandHandler(
            IClanRepository clanRepository,
            IClanRequestRepository requestRepository,
            IUnitOfWork unitOfWork,
            TimeProvider timeProvider,
            ILogger<CancelClanRequestCommandHandler> logger)
        {
            _clanRepository = clanRepository;
            _requestRepository = requestRepository;
            _unitOfWork = unitOfWork;
            _timeProvider = timeProvider;
            _logger = logger;
        }

        public async Task Handle(CancelClanRequestCommand request, CancellationToken cancellationToken)
        {
            var now = _timeProvider.GetUtcNow().UtcDateTime;

            var clanRequest = await _requestRepository.GetByIdAsync(request.RequestId, cancellationToken)
                ?? throw new EntityNotFoundException("Clan request", request.RequestId);

            if (clanRequest.Kind == ClanRequestKind.Application)
            {
                // Заявку знімає той, хто подав
                if (clanRequest.PlayerId != request.PlayerId)
                    throw new EntityNotFoundException("Clan request", request.RequestId);
            }
            else
            {
                var clan = await _clanRepository.GetByIdAsync(clanRequest.ClanId, cancellationToken)
                    ?? throw new EntityNotFoundException("Clan", clanRequest.ClanId);

                clan.EnsureCan(request.PlayerId, ClanPermission.Recruit);
            }

            clanRequest.Cancel(request.PlayerId, now);

            await _unitOfWork.SaveChangesAsync(cancellationToken);

            _logger.LogInformation("Clan request {RequestId} cancelled by {ActorId}",
                clanRequest.Id, request.PlayerId);
        }
    }
}
