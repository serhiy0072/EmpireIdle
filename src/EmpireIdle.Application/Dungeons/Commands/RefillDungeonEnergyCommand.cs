using EmpireIdle.Application.Common.Security;
using EmpireIdle.Application.Interfaces;
using EmpireIdle.Domain.Exceptions;
using EmpireIdle.Domain.Services;
using EmpireIdle.Domain.ValueObjects;
using MediatR;
using Microsoft.Extensions.Logging;

namespace EmpireIdle.Application.Dungeons.Commands
{
    /// <summary>
    /// Поповнення енергії данжів до стелі за gems (рішення 12.10.2026): ціна росте з кожним
    /// поповненням доби UTC, кількість цін у конфігу — денний ліміт.
    /// </summary>
    public record RefillDungeonEnergyCommand(Guid PlayerId) : IRequest, IPlayerScopedRequest, IIdempotentRequest;

    public sealed class RefillDungeonEnergyCommandHandler : IRequestHandler<RefillDungeonEnergyCommand>
    {
        private readonly IDungeonRepository _dungeons;
        private readonly IPlayerRepository _playerRepository;
        private readonly IPlayerWalletRepository _walletRepository;
        private readonly IUnitOfWork _unitOfWork;
        private readonly TimeProvider _timeProvider;
        private readonly GameCatalog _catalog;
        private readonly ILogger<RefillDungeonEnergyCommandHandler> _logger;

        public RefillDungeonEnergyCommandHandler(
            IDungeonRepository dungeons,
            IPlayerRepository playerRepository,
            IPlayerWalletRepository walletRepository,
            IUnitOfWork unitOfWork,
            TimeProvider timeProvider,
            GameCatalog catalog,
            ILogger<RefillDungeonEnergyCommandHandler> logger)
        {
            _dungeons = dungeons;
            _playerRepository = playerRepository;
            _walletRepository = walletRepository;
            _unitOfWork = unitOfWork;
            _timeProvider = timeProvider;
            _catalog = catalog;
            _logger = logger;
        }

        public async Task Handle(RefillDungeonEnergyCommand request, CancellationToken cancellationToken)
        {
            var now = _timeProvider.GetUtcNow().UtcDateTime;
            var settings = _catalog.Config.Dungeons;
            var prices = settings.RefillPricesGems;

            // Хто ще не заходив у данж, має повну шкалу — поповнювати нічого
            var energy = await _dungeons.GetEnergyAsync(request.PlayerId, cancellationToken)
                ?? throw new InvalidStateException(RefusalReasons.DungeonEnergyFull, $"Player {request.PlayerId} has full dungeon energy.");

            // Перевірка до оплати: повна шкала чи вичерпаний ліміт не мають коштувати gems
            energy.EnsureRefillable(settings.MaxEnergy, settings.RegenHours, prices.Count, now);

            var price = prices[energy.RefillsToday(now)];

            await PayAsync(request.PlayerId, price, now, cancellationToken);

            energy.RefillToCap(settings.MaxEnergy, settings.RegenHours, prices.Count, now);

            await _unitOfWork.SaveChangesAsync(cancellationToken);

            _logger.LogInformation("Player {PlayerId} refilled dungeon energy for {Gems} gems", request.PlayerId, price);
        }

        private async Task PayAsync(Guid playerId, int price, DateTime now, CancellationToken cancellationToken)
        {
            var player = await _playerRepository.GetByIdAsync(playerId, cancellationToken)
                ?? throw new EntityNotFoundException("Player", playerId);

            // Гаманець належить акаунту, тож ідемо через Player за UserId
            var wallet = await _walletRepository.GetByUserIdAsync(player.UserId, cancellationToken)
                ?? throw new EntityNotFoundException("Wallet", player.UserId);

            // Перевірка до списання: гравцю потрібна відмова з цифрами, а не виняток value object
            if (wallet.GemBalance.Value < price)
                throw new NotEnoughResourcesException("gems", price, wallet.GemBalance.Value);

            wallet.SpendGems(new GemAmount(price), "dungeon-energy-refill", playerId, now);
        }
    }
}
