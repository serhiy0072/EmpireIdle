using EmpireIdle.Application.Common.Security;
using EmpireIdle.Application.Interfaces;
using EmpireIdle.Domain.Services;
using EmpireIdle.Domain.ValueObjects;
using MediatR;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System.Net.WebSockets;

namespace EmpireIdle.Application.Villages.Commands
{
    /// <summary>Миттєво завершити будівництво за gems.</summary>
    public record SpeedUpConstructionCommand(Guid PlayerId, Guid BuildingId)
        : IRequest, IPlayerScopedRequest, IIdempotentRequest;

    public class SpeedUpConstructionCommandHandler : IRequestHandler<SpeedUpConstructionCommand>
    {

        private readonly IVillageRepository _villageRepository;
        private readonly IPlayerWalletRepository _walletRepository;
        private readonly IUnitOfWork _unitOfWork;
        private readonly SpeedUpCalculator _calculator;
        private readonly GameConfig _gameConfig;
        private readonly ILogger<SpeedUpConstructionCommandHandler> _logger;

        public SpeedUpConstructionCommandHandler(
            IVillageRepository villageRepository,IPlayerWalletRepository walletRepository,IUnitOfWork unitOfWork,
            SpeedUpCalculator calculator,IOptions<GameConfig> gameConfig,ILogger<SpeedUpConstructionCommandHandler> logger)
        {
            _villageRepository = villageRepository;
            _walletRepository = walletRepository;
            _unitOfWork = unitOfWork;
            _calculator = calculator;
            _gameConfig = gameConfig.Value;
            _logger = logger;
        }

        public async Task Handle(SpeedUpConstructionCommand request, CancellationToken cancellationToken)
        {
            var now = DateTime.UtcNow;
            var village = await _villageRepository.GetByPlayerIdAsync(request.PlayerId, cancellationToken)
                ?? throw new InvalidOperationException($"Village not found for player {request.PlayerId}.");

            var building = village.Buildings.FirstOrDefault(b=> b.Id == request.BuildingId)
                ?? throw new InvalidOperationException($"Building {request.BuildingId} not found.");

            if(!building.IsUnderConstruction)
                throw new InvalidOperationException("Building is not under construction.");

            var cost = _calculator.GetInstantFinishCost(building.ConstructionCompletesAt!.Value, now);

            if(cost>0)
            {
                var wallet = await _walletRepository.GetByPlayerIdAsync(request.PlayerId, cancellationToken)
                    ?? throw new InvalidOperationException($"Wallet not found for player {request.PlayerId}.");

                wallet.SpendGems(new GemAmount(cost), $"Speed up construction of {building.Type}");
            }

            // Завершуємо одразу, не чекаючи сканера
            var buildingsConfig = _gameConfig.Buildings.ToDictionary(b => b.Key, b => b);
            building.ReduceConstructionTime(building.ConstructionCompletesAt.Value - now);
            village.CompleteDueConstructions(now, buildingsConfig);

            await _unitOfWork.SaveChangesAsync(cancellationToken);

            _logger.LogInformation("Player {PlayerId} sped up building {BuildingId} for {Cost} gems", request.PlayerId, request.BuildingId, cost);
        }
    }
}
