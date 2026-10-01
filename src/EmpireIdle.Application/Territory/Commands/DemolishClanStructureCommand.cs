using EmpireIdle.Application.Common.Security;
using EmpireIdle.Application.Interfaces;
using EmpireIdle.Application.Territory.Services;
using EmpireIdle.Domain.Enums;
using EmpireIdle.Domain.Exceptions;
using MediatR;
using Microsoft.Extensions.Logging;

namespace EmpireIdle.Application.Territory.Commands
{
    /// <summary>
    /// Зносить власну споруду клану: так переносять територію (GDD §7.2 — лише
    /// знести й збудувати наново). Гарнізон іде додому, слот звільняється,
    /// витрачені очки вкладу не повертаються.
    /// </summary>
    public record DemolishClanStructureCommand(Guid PlayerId, Guid StructureId)
        : IRequest, IPlayerScopedRequest, IIdempotentRequest;

    public sealed class DemolishClanStructureCommandHandler : IRequestHandler<DemolishClanStructureCommand>
    {
        private readonly IClanRepository _clanRepository;
        private readonly IClanStructureRepository _structureRepository;
        private readonly ClanStructureRemover _remover;
        private readonly IUnitOfWork _unitOfWork;
        private readonly TimeProvider _timeProvider;
        private readonly ILogger<DemolishClanStructureCommandHandler> _logger;

        public DemolishClanStructureCommandHandler(
            IClanRepository clanRepository,
            IClanStructureRepository structureRepository,
            ClanStructureRemover remover,
            IUnitOfWork unitOfWork,
            TimeProvider timeProvider,
            ILogger<DemolishClanStructureCommandHandler> logger)
        {
            _clanRepository = clanRepository;
            _structureRepository = structureRepository;
            _remover = remover;
            _unitOfWork = unitOfWork;
            _timeProvider = timeProvider;
            _logger = logger;
        }

        public async Task Handle(DemolishClanStructureCommand request, CancellationToken cancellationToken)
        {
            var now = _timeProvider.GetUtcNow().UtcDateTime;

            var clan = await _clanRepository.GetByMemberAsync(request.PlayerId, cancellationToken)
                ?? throw new RequirementNotMetException(RefusalReasons.ClanNotMember, "Only clan members can demolish structures.");

            clan.EnsureCan(request.PlayerId, ClanPermission.BuildStructures);

            // Чужа споруда для гравця не існує: зносять лише свої, чужі — руйнують у бою
            var structure = await _structureRepository.GetByIdAsync(request.StructureId, cancellationToken);

            if (structure is null || structure.ClanId != clan.Id)
                throw new EntityNotFoundException("Clan structure", request.StructureId);

            await _remover.RemoveAsync(structure, now, cancellationToken);

            await _unitOfWork.SaveChangesAsync(cancellationToken);

            _logger.LogInformation("Clan {ClanId} demolished structure {StructureId}", clan.Id, structure.Id);
        }
    }
}
