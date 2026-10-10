using EmpireIdle.Application.Common.Security;
using EmpireIdle.Application.Interfaces;
using EmpireIdle.Application.Rewards;
using EmpireIdle.Application.Shop.Services;
using EmpireIdle.Domain.Services.Config;
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
        private readonly ShopCheckout _checkout;
        private readonly RewardDispatcher _dispatcher;
        private readonly IUnitOfWork _unitOfWork;
        private readonly TimeProvider _timeProvider;
        private readonly ILogger<BuyShopItemCommandHandler> _logger;

        public BuyShopItemCommandHandler(
            ShopCheckout checkout,
            RewardDispatcher dispatcher,
            IUnitOfWork unitOfWork,
            TimeProvider timeProvider,
            ILogger<BuyShopItemCommandHandler> logger)
        {
            _checkout = checkout;
            _dispatcher = dispatcher;
            _unitOfWork = unitOfWork;
            _timeProvider = timeProvider;
            _logger = logger;
        }

        public async Task<int> Handle(BuyShopItemCommand request, CancellationToken cancellationToken)
        {
            var (offer, item) = _checkout.Find(request.ItemKey);

            var now = _timeProvider.GetUtcNow().UtcDateTime;
            var charge = await _checkout.ChargeAsync(request.PlayerId, offer, item, request.Count, now, cancellationToken);

            await _dispatcher.GrantAllAsync(
                request.PlayerId,
                [new RewardConfig { Type = "Item", Key = offer.ItemKey, Amount = request.Count }],
                charge.Reference,
                now,
                cancellationToken);

            await _unitOfWork.SaveChangesAsync(cancellationToken);

            _logger.LogInformation("Player {PlayerId} bought {Count} × {ItemKey} for {Gems} gems",
                request.PlayerId, request.Count, offer.ItemKey, offer.PriceGems * request.Count);

            return charge.Wallet.GemBalance.Value;
        }
    }
}
