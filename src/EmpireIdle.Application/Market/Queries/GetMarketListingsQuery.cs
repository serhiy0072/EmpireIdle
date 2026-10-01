using EmpireIdle.Application.Common.Security;
using EmpireIdle.Application.Interfaces;
using EmpireIdle.Application.Market.Contracts;
using EmpireIdle.Application.Market.Services;
using EmpireIdle.Domain.Enums;
using EmpireIdle.Domain.Services;
using MediatR;

namespace EmpireIdle.Application.Market.Queries
{
    /// <summary>Сторінка вітрини: активні лоти світу, найдешевші за одиницю першими.</summary>
    public record GetMarketListingsQuery(Guid PlayerId, MarketListingKind? Kind, string? ItemKey, int Page, int PageSize)
        : IRequest<MarketPageView>, IPlayerScopedRequest
    {
        public const int MaxPageSize = 50;

        /// <summary>Стеля номера сторінки: (int.MaxValue - 1) * size переповнюється у від'ємний OFFSET.</summary>
        public const int MaxPage = 10_000;
    }

    public sealed class GetMarketListingsQueryHandler : IRequestHandler<GetMarketListingsQuery, MarketPageView>
    {
        private readonly IMarketRepository _market;
        private readonly MarketListingProjection _projection;
        private readonly TimeProvider _timeProvider;

        public GetMarketListingsQueryHandler(IMarketRepository market, MarketListingProjection projection, TimeProvider timeProvider)
        {
            _market = market;
            _projection = projection;
            _timeProvider = timeProvider;
        }

        public async Task<MarketPageView> Handle(GetMarketListingsQuery request, CancellationToken cancellationToken)
        {
            var now = _timeProvider.GetUtcNow().UtcDateTime;
            var page = Math.Clamp(request.Page, 1, GetMarketListingsQuery.MaxPage);
            var size = Math.Clamp(request.PageSize, 1, GetMarketListingsQuery.MaxPageSize);

            var (listings, total) = await _market.BrowseAsync(request.Kind, request.ItemKey, now,
                (page - 1) * size, size, cancellationToken);

            var views = await _projection.ProjectAsync(listings, request.PlayerId, cancellationToken);

            return new MarketPageView(views, total, page, size);
        }
    }
}
