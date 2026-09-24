using EmpireIdle.Application.Common.Security;
using EmpireIdle.Application.Interfaces;
using EmpireIdle.Application.Market.Contracts;
using EmpireIdle.Application.Market.Services;
using EmpireIdle.Domain.Enums;
using EmpireIdle.Domain.Services;
using MediatR;

namespace EmpireIdle.Application.Market.Queries
{
    /// <summary>Котирування для форми виставлення: діапазон ціни для конкретного товару.</summary>
    public record GetMarketQuoteQuery(Guid PlayerId, MarketListingKind Kind, Guid? EquipmentId, Guid? HeroId,
        string? ItemKey, int Quantity) : IRequest<MarketQuoteView>, IPlayerScopedRequest;

    public sealed class GetMarketQuoteQueryHandler : IRequestHandler<GetMarketQuoteQuery, MarketQuoteView>
    {
        private readonly MarketGoods _goods;
        private readonly MarketDesk _desk;
        private readonly GameCatalog _catalog;

        public GetMarketQuoteQueryHandler(MarketGoods goods, MarketDesk desk, GameCatalog catalog)
        {
            _goods = goods;
            _desk = desk;
            _catalog = catalog;
        }

        public async Task<MarketQuoteView> Handle(GetMarketQuoteQuery request, CancellationToken cancellationToken)
        {
            // Та сама оцінка, що й при виставленні: форма не може показати діапазон,
            // якого команда потім не прийме
            var goods = await _goods.AppraiseAsync(request.PlayerId, request.Kind, request.EquipmentId, request.HeroId,
                request.ItemKey, Math.Max(1, request.Quantity), cancellationToken);

            var corridor = await _desk.CorridorAsync(goods.PricingKey, cancellationToken);

            var min = corridor.MinPrice(goods.Units);
            var max = corridor.MaxPrice(goods.Units);

            return new MarketQuoteView(goods.Units, min, max, (min + max) / 2, _catalog.Config.Market.ListingTaxShare);
        }
    }
}
