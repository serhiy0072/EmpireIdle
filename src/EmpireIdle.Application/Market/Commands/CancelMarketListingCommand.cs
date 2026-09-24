using EmpireIdle.Application.Common.Security;
using EmpireIdle.Application.Interfaces;
using EmpireIdle.Application.Market.Services;
using EmpireIdle.Domain.Exceptions;
using MediatR;
using Microsoft.Extensions.Logging;

namespace EmpireIdle.Application.Market.Commands
{
    /// <summary>Зняти власний лот. Товар повертається, податок — ні.</summary>
    public record CancelMarketListingCommand(Guid PlayerId, Guid ListingId)
        : IRequest, IPlayerScopedRequest, IIdempotentRequest;

    public sealed class CancelMarketListingCommandHandler : IRequestHandler<CancelMarketListingCommand>
    {
        private readonly IMarketRepository _market;
        private readonly MarketGoods _goods;
        private readonly IUnitOfWork _unitOfWork;
        private readonly TimeProvider _timeProvider;
        private readonly ILogger<CancelMarketListingCommandHandler> _logger;

        public CancelMarketListingCommandHandler(
            IMarketRepository market,
            MarketGoods goods,
            IUnitOfWork unitOfWork,
            TimeProvider timeProvider,
            ILogger<CancelMarketListingCommandHandler> logger)
        {
            _market = market;
            _goods = goods;
            _unitOfWork = unitOfWork;
            _timeProvider = timeProvider;
            _logger = logger;
        }

        public async Task Handle(CancelMarketListingCommand request, CancellationToken cancellationToken)
        {
            var now = _timeProvider.GetUtcNow().UtcDateTime;

            var listing = await _market.GetByIdAsync(request.ListingId, cancellationToken);

            // Чужий лот не відрізняється від неіснуючого
            if (listing is null || listing.SellerId != request.PlayerId)
                throw new EntityNotFoundException("Market listing", request.ListingId.ToString());

            listing.Cancel(now);

            await _goods.ReturnToSellerAsync(listing, now, cancellationToken);
            await _unitOfWork.SaveChangesAsync(cancellationToken);

            _logger.LogInformation("Player {PlayerId} cancelled listing {ListingId}", request.PlayerId, listing.Id);
        }
    }
}
