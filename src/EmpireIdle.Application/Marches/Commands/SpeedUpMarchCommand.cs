using EmpireIdle.Application.Common.Security;
using EmpireIdle.Application.Interfaces;
using EmpireIdle.Domain.Services;
using EmpireIdle.Domain.ValueObjects;
using MediatR;
using Microsoft.Extensions.Logging;

namespace EmpireIdle.Application.Marches.Commands
{
    /// <summary>Миттєво завершити переміщення армії за gems.</summary>
    public record SpeedUpMarchCommand(Guid PlayerId, Guid MarchId)
        : IRequest, IPlayerScopedRequest, IIdempotentRequest;

    /// <summary>
    /// Обробник SpeedUpMarchCommand: списує gems і зсуває час прибуття на «зараз».
    /// Сам бій відбудеться найближчим проходом сканера.
    /// </summary>
    public class SpeedUpMarchCommandHandler : IRequestHandler<SpeedUpMarchCommand>
    {
        private readonly IVillageRepository _villageRepository;
        private readonly IGarrisonRepository _garrisonRepository;
        private readonly IMarchRepository _marchRepository;
        private readonly IPlayerWalletRepository _walletRepository;
        private readonly IUnitOfWork _unitOfWork;
        private readonly SpeedUpCalculator _calculator;
        private readonly ILogger<SpeedUpMarchCommandHandler> _logger;

        public SpeedUpMarchCommandHandler(
            IVillageRepository villageRepository,
            IGarrisonRepository garrisonRepository,
            IMarchRepository marchRepository,
            IPlayerWalletRepository walletRepository,
            IUnitOfWork unitOfWork,
            SpeedUpCalculator calculator,
            ILogger<SpeedUpMarchCommandHandler> logger)
        {
            _villageRepository = villageRepository;
            _garrisonRepository = garrisonRepository;
            _marchRepository = marchRepository;
            _walletRepository = walletRepository;
            _unitOfWork = unitOfWork;
            _calculator = calculator;
            _logger = logger;
        }

        public async Task Handle(SpeedUpMarchCommand request, CancellationToken cancellationToken)
        {
            var now = DateTime.UtcNow;

            var village = await _villageRepository.GetByPlayerIdAsync(request.PlayerId, cancellationToken)
                ?? throw new InvalidOperationException($"Village not found for player {request.PlayerId}.");

            var garrison = await _garrisonRepository.GetByVillageIdAsync(village.Id, cancellationToken)
                ?? throw new InvalidOperationException($"Garrison not found for village {village.Id}.");

            // Шукаємо серед активних походів цього гарнізону — так марш чужого гравця не прискорити
            var marches = await _marchRepository.GetActiveByGarrisonAsync(garrison.Id, cancellationToken);

            var march = marches.FirstOrDefault(m => m.Id == request.MarchId)
                ?? throw new InvalidOperationException($"Active march {request.MarchId} not found.");

            var cost = _calculator.GetInstantFinishCost(march.ArrivesAt, now);

            if (cost > 0)
            {
                var wallet = await _walletRepository.GetByPlayerIdAsync(request.PlayerId, cancellationToken)
                    ?? throw new InvalidOperationException($"Wallet not found for player {request.PlayerId}.");

                wallet.SpendGems(new GemAmount(cost), "Speed up march");
            }

            // Зсуваємо прибуття на «зараз»; бій або повернення відпрацює сканер
            march.Reduce(march.ArrivesAt - now);

            await _unitOfWork.SaveChangesAsync(cancellationToken);

            _logger.LogInformation("Player {PlayerId} sped up march {MarchId} for {Cost} gems",
                request.PlayerId, request.MarchId, cost);
        }
    }
}