using EmpireIdle.Application.Common.Security;
using EmpireIdle.Application.Interfaces;
using EmpireIdle.Application.Marches.Services;
using EmpireIdle.Domain.Exceptions;
using MediatR;
using Microsoft.Extensions.Logging;

namespace EmpireIdle.Application.Marches.Commands
{
    /// <summary>Відкликати табір (§2.5): армія йде додому звичайним маршем.</summary>
    public record RecallCampCommand(Guid PlayerId, Guid MarchId)
        : IRequest, IPlayerScopedRequest, IIdempotentRequest;

    /// <summary>Обробник RecallCampCommand: дорогу рахує CampHomecoming — той самий шлях, що й відступ після бою.</summary>
    public sealed class RecallCampCommandHandler : IRequestHandler<RecallCampCommand>
    {
        private readonly IVillageRepository _villageRepository;
        private readonly IGarrisonRepository _garrisonRepository;
        private readonly IMarchRepository _marchRepository;
        private readonly IUnitOfWork _unitOfWork;
        private readonly TimeProvider _timeProvider;
        private readonly CampHomecoming _homecoming;
        private readonly ILogger<RecallCampCommandHandler> _logger;

        public RecallCampCommandHandler(
            IVillageRepository villageRepository,
            IGarrisonRepository garrisonRepository,
            IMarchRepository marchRepository,
            IUnitOfWork unitOfWork,
            TimeProvider timeProvider,
            CampHomecoming homecoming,
            ILogger<RecallCampCommandHandler> logger)
        {
            _villageRepository = villageRepository;
            _garrisonRepository = garrisonRepository;
            _marchRepository = marchRepository;
            _unitOfWork = unitOfWork;
            _timeProvider = timeProvider;
            _homecoming = homecoming;
            _logger = logger;
        }

        public async Task Handle(RecallCampCommand request, CancellationToken cancellationToken)
        {
            var now = _timeProvider.GetUtcNow().UtcDateTime;

            var village = await _villageRepository.GetByPlayerIdAsync(request.PlayerId, cancellationToken)
                ?? throw new InvalidOperationException($"Village not found for player {request.PlayerId}.");

            var garrison = await _garrisonRepository.GetByVillageIdAsync(village.Id, cancellationToken)
                ?? throw new InvalidOperationException($"Garrison not found for village {village.Id}.");

            // Шукаємо серед активних походів цього гарнізону — так чужий табір не відкликати
            var marches = await _marchRepository.GetActiveByGarrisonAsync(garrison.Id, cancellationToken);

            var march = marches.FirstOrDefault(m => m.Id == request.MarchId)
                ?? throw new EntityNotFoundException("Active", request.MarchId);

            var duration = await _homecoming.SendHomeAsync(march, village, now, cancellationToken);

            await _unitOfWork.SaveChangesAsync(cancellationToken);

            _logger.LogInformation("Player {PlayerId} recalled camp {MarchId} from ({X},{Y}), home in {Minutes:F1} min",
                request.PlayerId, march.Id, march.TargetX, march.TargetY, duration.TotalMinutes);
        }
    }
}
