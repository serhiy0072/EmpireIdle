using EmpireIdle.Application.Common.Security;
using EmpireIdle.Application.Interfaces;
using EmpireIdle.Domain.Enums;
using EmpireIdle.Domain.Exceptions;
using EmpireIdle.Domain.Services;
using EmpireIdle.Domain.ValueObjects;
using MediatR;
using Microsoft.Extensions.Logging;

namespace EmpireIdle.Application.Marches.Commands
{
    /// <summary>Прискорити марш за gems: зрізати Percent відсотків залишку дороги (варіант із конфіга).</summary>
    public record SpeedUpMarchCommand(Guid PlayerId, Guid MarchId, int Percent)
        : IRequest, IPlayerScopedRequest, IIdempotentRequest;

    /// <summary>
    /// Обробник SpeedUpMarchCommand: списує сталу ціну варіанта й зрізає його частку залишку дороги,
    /// не нижче межі прискорення. Сам бій відбудеться найближчим проходом сканера.
    /// </summary>
    public sealed class SpeedUpMarchCommandHandler : IRequestHandler<SpeedUpMarchCommand>
    {
        private readonly IVillageRepository _villageRepository;
        private readonly IGarrisonRepository _garrisonRepository;
        private readonly IMarchRepository _marchRepository;
        private readonly IPlayerWalletRepository _walletRepository;
        private readonly ICurrentPlayer _currentPlayer;
        private readonly IUnitOfWork _unitOfWork;
        private readonly TimeProvider _timeProvider;
        private readonly SpeedUpCalculator _calculator;
        private readonly ILogger<SpeedUpMarchCommandHandler> _logger;

        public SpeedUpMarchCommandHandler(
            IVillageRepository villageRepository,
            IGarrisonRepository garrisonRepository,
            IMarchRepository marchRepository,
            IPlayerWalletRepository walletRepository,
            ICurrentPlayer currentPlayer,
            IUnitOfWork unitOfWork,
            TimeProvider timeProvider,
            SpeedUpCalculator calculator,
            ILogger<SpeedUpMarchCommandHandler> logger)
        {
            _villageRepository = villageRepository;
            _garrisonRepository = garrisonRepository;
            _marchRepository = marchRepository;
            _walletRepository = walletRepository;
            _currentPlayer = currentPlayer;
            _timeProvider = timeProvider;
            _unitOfWork = unitOfWork;
            _calculator = calculator;
            _logger = logger;
        }

        public async Task Handle(SpeedUpMarchCommand request, CancellationToken cancellationToken)
        {
            var now = _timeProvider.GetUtcNow().UtcDateTime;

            var village = await _villageRepository.GetByPlayerIdAsync(request.PlayerId, cancellationToken)
                ?? throw new InvalidOperationException($"Village not found for player {request.PlayerId}.");

            var garrison = await _garrisonRepository.GetByVillageIdAsync(village.Id, cancellationToken)
                ?? throw new InvalidOperationException($"Garrison not found for village {village.Id}.");

            // Шукаємо серед активних походів цього гарнізону — так марш чужого гравця не прискорити
            var marches = await _marchRepository.GetActiveByGarrisonAsync(garrison.Id, cancellationToken);

            var march = marches.FirstOrDefault(m => m.Id == request.MarchId)
                ?? throw new EntityNotFoundException($"Active", request.MarchId);

            // Варіант перевірив валідатор; останні секунди маршу прискорення не зрізає — бій чи повернення проведе сканер
            var option = _calculator.MarchOption(request.Percent)
                ?? throw new InvalidOperationException($"Unknown march speed-up option {request.Percent}%.");
            var cut = _calculator.RequireMarchCut(option.Percent, march.ArrivesAt, now);
            var cost = option.Gems;

            var userId = _currentPlayer.UserId
                ?? throw new UnauthorizedAccessException("This operation requires an authenticated account.");

            var wallet = await _walletRepository.GetByUserIdAsync(userId, cancellationToken)
                ?? throw new InvalidOperationException($"Wallet not found.");

            wallet.SpendGems(new GemAmount(cost), "Speed up march", request.PlayerId, now);

            march.ReduceTravelTime(cut, now);

            await _unitOfWork.SaveChangesAsync(cancellationToken);

            _logger.LogInformation("Player {PlayerId} sped up march {MarchId} by {Percent}% for {Cost} gems",
                request.PlayerId, request.MarchId, option.Percent, cost);
        }
    }
}
