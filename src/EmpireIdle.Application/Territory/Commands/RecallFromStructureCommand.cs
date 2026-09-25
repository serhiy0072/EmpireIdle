using EmpireIdle.Application.Clans.Services;
using EmpireIdle.Application.Common.Security;
using EmpireIdle.Application.Interfaces;
using EmpireIdle.Domain.Exceptions;
using MediatR;
using Microsoft.Extensions.Logging;

namespace EmpireIdle.Application.Territory.Commands
{
    /// <summary>
    /// Забирає юнітів і героїв гравця з гарнізону однієї споруди. Автоматично вони
    /// не повертаються: споруда без гарнізону падає з першого нальоту (GDD §7.2).
    /// </summary>
    public record RecallFromStructureCommand(Guid PlayerId, Guid StructureId)
        : IRequest<bool>, IPlayerScopedRequest, IIdempotentRequest;

    public sealed class RecallFromStructureCommandHandler : IRequestHandler<RecallFromStructureCommand, bool>
    {
        private readonly IClanStructureRepository _structureRepository;
        private readonly IGarrisonRepository _garrisonRepository;
        private readonly ReinforcementReturner _returner;
        private readonly IUnitOfWork _unitOfWork;
        private readonly TimeProvider _timeProvider;
        private readonly ILogger<RecallFromStructureCommandHandler> _logger;

        public RecallFromStructureCommandHandler(
            IClanStructureRepository structureRepository,
            IGarrisonRepository garrisonRepository,
            ReinforcementReturner returner,
            IUnitOfWork unitOfWork,
            TimeProvider timeProvider,
            ILogger<RecallFromStructureCommandHandler> logger)
        {
            _structureRepository = structureRepository;
            _garrisonRepository = garrisonRepository;
            _returner = returner;
            _unitOfWork = unitOfWork;
            _timeProvider = timeProvider;
            _logger = logger;
        }

        /// <returns>true, якщо марш додому вирушив; false — у споруді нічого гравцевого немає.</returns>
        public async Task<bool> Handle(RecallFromStructureCommand request, CancellationToken cancellationToken)
        {
            var now = _timeProvider.GetUtcNow().UtcDateTime;

            var structure = await _structureRepository.GetByIdAsync(request.StructureId, cancellationToken)
                ?? throw new EntityNotFoundException("Clan structure", request.StructureId);

            var garrison = await _garrisonRepository.GetByIdAsync(structure.GarrisonId, cancellationToken)
                ?? throw new EntityNotFoundException("Clan structure", request.StructureId);

            var sent = await _returner.ReturnOwnerAsync(garrison, request.PlayerId, now, cancellationToken);

            await _unitOfWork.SaveChangesAsync(cancellationToken);

            _logger.LogInformation("Player {PlayerId} recalled from structure {StructureId}: {Sent}",
                request.PlayerId, structure.Id, sent);

            return sent;
        }
    }
}
