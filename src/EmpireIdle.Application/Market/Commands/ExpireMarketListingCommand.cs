using EmpireIdle.Application.Interfaces;
using EmpireIdle.Application.Market.Services;
using EmpireIdle.Domain.Enums;
using MediatR;
using Microsoft.Extensions.Logging;

namespace EmpireIdle.Application.Market.Commands
{
    /// <summary>
    /// Закриває лот, строк якого минув, і повертає товар продавцю.
    /// Системна команда сканера, не дія гравця.
    /// </summary>
    public record ExpireMarketListingCommand(Guid ListingId) : IRequest;

    public sealed class ExpireMarketListingCommandHandler : IRequestHandler<ExpireMarketListingCommand>
    {
        private readonly IMarketRepository _market;
        private readonly MarketGoods _goods;
        private readonly IUnitOfWork _unitOfWork;
        private readonly TimeProvider _timeProvider;
        private readonly ILogger<ExpireMarketListingCommandHandler> _logger;

        public ExpireMarketListingCommandHandler(
            IMarketRepository market,
            MarketGoods goods,
            IUnitOfWork unitOfWork,
            TimeProvider timeProvider,
            ILogger<ExpireMarketListingCommandHandler> logger)
        {
            _market = market;
            _goods = goods;
            _unitOfWork = unitOfWork;
            _timeProvider = timeProvider;
            _logger = logger;
        }

        public async Task Handle(ExpireMarketListingCommand request, CancellationToken cancellationToken)
        {
            var now = _timeProvider.GetUtcNow().UtcDateTime;

            var listing = await _market.GetByIdAsync(request.ListingId, cancellationToken);

            // Між вибіркою сканера й обробкою лот могли купити чи зняти — це не помилка
            if (listing is null || listing.State != MarketListingState.Active || listing.ExpiresAt > now)
                return;

            listing.Expire(now);

            await _goods.ReturnToSellerAsync(listing, now, cancellationToken);
            await _unitOfWork.SaveChangesAsync(cancellationToken);

            _logger.LogInformation("Listing {ListingId} expired and returned to {SellerId}", listing.Id, listing.SellerId);
        }
    }
}
