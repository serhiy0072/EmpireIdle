using EmpireIdle.Application.Common.Security;
using EmpireIdle.Application.Interfaces;
using EmpireIdle.Domain.Services;
using MediatR;
using Microsoft.Extensions.Logging;

namespace EmpireIdle.Application.Villages.Commands
{
    /// <summary>
    /// Команда: переставити будівлю на плані села (рішення 13.10.2026). Правила плану —
    /// сітка, мур, без накладання — перевіряє село.
    /// </summary>
    public record MoveBuildingCommand(Guid PlayerId, Guid BuildingId, double X, double Y)
        : IRequest, IPlayerScopedRequest, IIdempotentRequest;

    public sealed class MoveBuildingCommandHandler : IRequestHandler<MoveBuildingCommand>
    {
        private readonly IVillageRepository _villageRepository;
        private readonly IUnitOfWork _unitOfWork;
        private readonly GameCatalog _catalog;
        private readonly TimeProvider _timeProvider;
        private readonly ILogger<MoveBuildingCommandHandler> _logger;

        public MoveBuildingCommandHandler(
            IVillageRepository villageRepository,
            IUnitOfWork unitOfWork,
            GameCatalog catalog,
            TimeProvider timeProvider,
            ILogger<MoveBuildingCommandHandler> logger)
        {
            _villageRepository = villageRepository;
            _unitOfWork = unitOfWork;
            _catalog = catalog;
            _timeProvider = timeProvider;
            _logger = logger;
        }

        public async Task Handle(MoveBuildingCommand request, CancellationToken cancellationToken)
        {
            var village = await _villageRepository.GetByPlayerIdAsync(request.PlayerId, cancellationToken)
                ?? throw new InvalidOperationException($"Village not found for player {request.PlayerId}.");

            village.MoveBuilding(request.BuildingId, request.X, request.Y, _catalog.Buildings,
                _timeProvider.GetUtcNow().UtcDateTime);

            await _unitOfWork.SaveChangesAsync(cancellationToken);

            _logger.LogInformation("Building {BuildingId} moved to ({X}, {Y}) for player {PlayerId}",
                request.BuildingId, request.X, request.Y, request.PlayerId);
        }
    }
}
