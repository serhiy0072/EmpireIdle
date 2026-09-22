using EmpireIdle.Application.Common.Security;
using EmpireIdle.Application.Interfaces;
using EmpireIdle.Application.Rewards;
using EmpireIdle.Domain.Exceptions;
using EmpireIdle.Domain.Services;
using EmpireIdle.Domain.Services.Config;
using EmpireIdle.Domain.ValueObjects;
using MediatR;
using Microsoft.Extensions.Logging;

namespace EmpireIdle.Application.Shop.Commands
{
    /// <summary>Купівля предмета крамниці за gems: списання й видача — одна транзакція.</summary>
    public record BuyShopItemCommand(Guid PlayerId, string ItemKey, int Count = 1)
        : IRequest<int>, IPlayerScopedRequest, IIdempotentRequest;

    /// <summary>Повертає залишок gems після покупки — клієнту не треба перепитувати гаманець.</summary>
    internal sealed class BuyShopItemCommandHandler : IRequestHandler<BuyShopItemCommand, int>
    {
        private readonly IPlayerRepository _playerRepository;
        private readonly IPlayerWalletRepository _walletRepository;
        private readonly GameCatalog _catalog;
        private readonly RewardDispatcher _dispatcher;
        private readonly IUnitOfWork _unitOfWork;
        private readonly TimeProvider _timeProvider;
        private readonly ILogger<BuyShopItemCommandHandler> _logger;

        public BuyShopItemCommandHandler(
            IPlayerRepository playerRepository,
            IPlayerWalletRepository walletRepository,
            GameCatalog catalog,
            RewardDispatcher dispatcher,
            IUnitOfWork unitOfWork,
            TimeProvider timeProvider,
            ILogger<BuyShopItemCommandHandler> logger)
        {
            _playerRepository = playerRepository;
            _walletRepository = walletRepository;
            _catalog = catalog;
            _dispatcher = dispatcher;
            _unitOfWork = unitOfWork;
            _timeProvider = timeProvider;
            _logger = logger;
        }

        public async Task<int> Handle(BuyShopItemCommand request, CancellationToken cancellationToken)
        {
            var offer = _catalog.Config.Shop.Items.FirstOrDefault(i => i.ItemKey == request.ItemKey)
                ?? throw new EntityNotFoundException("Shop item", request.ItemKey);

            if (request.Count > offer.MaxPerPurchase)
                throw new RequirementNotMetException($"At most {offer.MaxPerPurchase} × '{offer.ItemKey}' per purchase.");

            var player = await _playerRepository.GetByIdAsync(request.PlayerId, cancellationToken)
                ?? throw new EntityNotFoundException("Player", request.PlayerId);

            // Гаманець належить акаунту, тож ідемо через Player за UserId
            var wallet = await _walletRepository.GetByUserIdAsync(player.UserId, cancellationToken)
                ?? throw new EntityNotFoundException("Wallet", player.UserId);

            var total = offer.PriceGems * request.Count;

            // Перевірка до списання: гравцю потрібна відмова з цифрами, а не виняток value object
            if (wallet.GemBalance.Value < total)
                throw new NotEnoughResourcesException("gems", total, wallet.GemBalance.Value);

            var now = _timeProvider.GetUtcNow().UtcDateTime;
            var reference = $"shop:{offer.ItemKey}";

            wallet.SpendGems(new GemAmount(total), reference, request.PlayerId, now);

            await _dispatcher.GrantAllAsync(
                request.PlayerId,
                [new RewardConfig { Type = "Item", Key = offer.ItemKey, Amount = request.Count }],
                reference,
                now,
                cancellationToken);

            await _unitOfWork.SaveChangesAsync(cancellationToken);

            _logger.LogInformation("Player {PlayerId} bought {Count} × {ItemKey} for {Gems} gems",
                request.PlayerId, request.Count, offer.ItemKey, total);

            return wallet.GemBalance.Value;
        }
    }
}
