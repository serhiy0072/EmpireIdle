using EmpireIdle.Application.Common.Security;
using EmpireIdle.Application.Interfaces;
using EmpireIdle.Application.Market.Services;
using EmpireIdle.Domain.Exceptions;
using EmpireIdle.Domain.Services;
using EmpireIdle.Domain.Services.Config;
using MediatR;
using Microsoft.Extensions.Logging;

namespace EmpireIdle.Application.Market.Commands
{
    /// <summary>Купити лот ринку за його ціну в золоті.</summary>
    public record BuyMarketListingCommand(Guid PlayerId, Guid ListingId)
        : IRequest, IPlayerScopedRequest, IIdempotentRequest;

    /// <summary>
    /// Одна транзакція на все: золото покупця, лот, товар і виручка продавця.
    /// Паралельну покупку того самого лота відсікає токен xmin — друга
    /// отримає конфлікт, а не другий екземпляр товару.
    ///
    /// Виручка йде на склад продавця повністю, навіть понад кап (GDD §8.8):
    /// продане не згорає.
    /// </summary>
    public sealed class BuyMarketListingCommandHandler : IRequestHandler<BuyMarketListingCommand>
    {
        private readonly IMarketRepository _market;
        private readonly IVillageRepository _villages;
        private readonly MarketGoods _goods;
        private readonly MarketDesk _desk;
        private readonly GameCatalog _catalog;
        private readonly IUnitOfWork _unitOfWork;
        private readonly TimeProvider _timeProvider;
        private readonly ILogger<BuyMarketListingCommandHandler> _logger;

        public BuyMarketListingCommandHandler(
            IMarketRepository market,
            IVillageRepository villages,
            MarketGoods goods,
            MarketDesk desk,
            GameCatalog catalog,
            IUnitOfWork unitOfWork,
            TimeProvider timeProvider,
            ILogger<BuyMarketListingCommandHandler> logger)
        {
            _market = market;
            _villages = villages;
            _goods = goods;
            _desk = desk;
            _catalog = catalog;
            _unitOfWork = unitOfWork;
            _timeProvider = timeProvider;
            _logger = logger;
        }

        public async Task Handle(BuyMarketListingCommand request, CancellationToken cancellationToken)
        {
            var now = _timeProvider.GetUtcNow().UtcDateTime;

            var listing = await _market.GetByIdAsync(request.ListingId, cancellationToken)
                ?? throw new EntityNotFoundException("Market listing", request.ListingId.ToString());

            var buyerVillage = await _villages.GetByPlayerIdAsync(request.PlayerId, cancellationToken)
                ?? throw new InvalidOperationException($"Village not found for player {request.PlayerId}.");

            // Купувати можна лише з відкритим ринком — так само, як і продавати
            _desk.RequireOpen(buyerVillage);

            // Стан лота — першим: «уже продано» зрозуміліше за «не вистачає золота»
            listing.Buy(request.PlayerId, now);

            buyerVillage.ChargeCost([new ResourceCost { Resource = "gold", Amount = listing.PriceGold }], now);

            await _goods.HandOverAsync(listing, request.PlayerId, now,
                TimeSpan.FromHours(_catalog.Config.Market.ResaleCooldownHours), cancellationToken);

            var sellerVillage = await _villages.GetByPlayerIdAsync(listing.SellerId, cancellationToken)
                ?? throw new InvalidOperationException($"Village not found for seller {listing.SellerId}.");

            sellerVillage.GrantResources([new ResourceCost { Resource = "gold", Amount = listing.PriceGold }], now);

            await _unitOfWork.SaveChangesAsync(cancellationToken);

            _logger.LogInformation("Player {BuyerId} bought listing {ListingId} ({ItemKey}) from {SellerId} for {Price} gold",
                request.PlayerId, listing.Id, listing.ItemKey, listing.SellerId, listing.PriceGold);
        }
    }
}
