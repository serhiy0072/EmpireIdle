using EmpireIdle.Application.Common.Security;
using EmpireIdle.Application.Interfaces;
using EmpireIdle.Application.Lootboxes.Contracts;
using EmpireIdle.Application.Lootboxes.Services;
using EmpireIdle.Application.Shop.Services;
using EmpireIdle.Domain.Exceptions;
using EmpireIdle.Domain.Services;
using MediatR;
using Microsoft.Extensions.Logging;

namespace EmpireIdle.Application.Lootboxes.Commands
{
    /// <summary>
    /// Купити скрині в крамниці й одразу відкрити (GDD §9.12), як крутку банера: у рюкзак
    /// куплена скриня не лягає. Списання gems, гарантія й видача — одна транзакція.
    /// </summary>
    public record BuyLootboxCommand(Guid PlayerId, string ItemKey, int Count = 1)
        : IRequest<LootboxPurchase>, IPlayerScopedRequest, IIdempotentRequest;

    internal sealed class BuyLootboxCommandHandler : IRequestHandler<BuyLootboxCommand, LootboxPurchase>
    {
        private readonly ShopCheckout _checkout;
        private readonly LootboxOpener _opener;
        private readonly GameCatalog _catalog;
        private readonly IUnitOfWork _unitOfWork;
        private readonly TimeProvider _timeProvider;
        private readonly ILogger<BuyLootboxCommandHandler> _logger;

        public BuyLootboxCommandHandler(
            ShopCheckout checkout,
            LootboxOpener opener,
            GameCatalog catalog,
            IUnitOfWork unitOfWork,
            TimeProvider timeProvider,
            ILogger<BuyLootboxCommandHandler> logger)
        {
            _checkout = checkout;
            _opener = opener;
            _catalog = catalog;
            _unitOfWork = unitOfWork;
            _timeProvider = timeProvider;
            _logger = logger;
        }

        public async Task<LootboxPurchase> Handle(BuyLootboxCommand request, CancellationToken cancellationToken)
        {
            var (offer, item) = _checkout.Find(request.ItemKey);

            // Звичайний товар іде в рюкзак іншою командою — тут лише скрині
            var box = _catalog.FindLootbox(item.Key)
                ?? throw new EntityNotFoundException("Lootbox", request.ItemKey);

            var now = _timeProvider.GetUtcNow().UtcDateTime;
            var charge = await _checkout.ChargeAsync(request.PlayerId, offer, item, request.Count, now, cancellationToken);

            var drops = await _opener.OpenAsync(request.PlayerId, box, request.Count, charge.Reference, now, cancellationToken);

            await _unitOfWork.SaveChangesAsync(cancellationToken);

            _logger.LogInformation("Player {PlayerId} bought and opened {Count} × {Lootbox} for {Gems} gems: {Drops}",
                request.PlayerId, request.Count, box.Key, offer.PriceGems * request.Count,
                string.Join(", ", drops.Select(d => $"{d.Type}:{d.Key}×{d.Amount}")));

            // Баланс після видачі: gems зі скрині вже лягли в той самий гаманець
            return new LootboxPurchase(box.Key, drops, charge.Wallet.GemBalance.Value);
        }
    }
}
