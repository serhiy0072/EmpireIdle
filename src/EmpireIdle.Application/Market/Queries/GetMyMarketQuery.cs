using EmpireIdle.Application.Common.Security;
using EmpireIdle.Application.Interfaces;
using EmpireIdle.Application.Market.Contracts;
using EmpireIdle.Application.Market.Services;
using EmpireIdle.Domain.Enums;
using EmpireIdle.Domain.Services;
using MediatR;

namespace EmpireIdle.Application.Market.Queries
{
    /// <summary>Стан ринку для гравця й його власні лоти.</summary>
    public record GetMyMarketQuery(Guid PlayerId) : IRequest<MyMarketView>, IPlayerScopedRequest
    {
        /// <summary>Скільки закритих лотів показувати поруч з активними.</summary>
        public const int ClosedToShow = 10;
    }

    public sealed class GetMyMarketQueryHandler : IRequestHandler<GetMyMarketQuery, MyMarketView>
    {
        private readonly IMarketRepository _market;
        private readonly IVillageRepository _villages;
        private readonly MarketDesk _desk;
        private readonly MarketListingProjection _projection;
        private readonly GameCatalog _catalog;

        public GetMyMarketQueryHandler(IMarketRepository market, IVillageRepository villages, MarketDesk desk,
            MarketListingProjection projection, GameCatalog catalog)
        {
            _market = market;
            _villages = villages;
            _desk = desk;
            _projection = projection;
            _catalog = catalog;
        }

        public async Task<MyMarketView> Handle(GetMyMarketQuery request, CancellationToken cancellationToken)
        {
            var village = await _villages.GetByPlayerIdReadOnlyAsync(request.PlayerId, cancellationToken)
                ?? throw new InvalidOperationException($"Village not found for player {request.PlayerId}.");

            var level = _desk.OpenLevel(village);

            var listings = await _market.GetBySellerAsync(request.PlayerId, GetMyMarketQuery.ClosedToShow, cancellationToken);
            var views = await _projection.ProjectAsync(listings, request.PlayerId, cancellationToken);

            var market = _catalog.Config.Market;

            return new MyMarketView(
                level is not null,
                _desk.OpensAtTownHall,
                level is { } open ? _desk.ListingLimit(open) : 0,
                listings.Count(l => l.State == MarketListingState.Active),
                market.ListingTaxShare,
                market.ListingHours,
                views);
        }
    }
}
