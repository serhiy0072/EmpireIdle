using EmpireIdle.Application.Common.Security;
using EmpireIdle.Application.Interfaces;
using EmpireIdle.Application.Market.Services;
using EmpireIdle.Domain.Entities;
using EmpireIdle.Domain.Enums;
using EmpireIdle.Domain.Exceptions;
using EmpireIdle.Domain.Services;
using EmpireIdle.Domain.Services.Config;
using MediatR;
using Microsoft.Extensions.Logging;

namespace EmpireIdle.Application.Market.Commands
{
    /// <summary>
    /// Виставити товар на ринок за фіксовану ціну в золоті (GDD §8.8).
    /// Заповнюється поле свого виду: EquipmentId, HeroId або ItemKey з Quantity.
    /// </summary>
    /// <returns>Id створеного лота.</returns>
    public record ListOnMarketCommand(
        Guid PlayerId,
        MarketListingKind Kind,
        Guid? EquipmentId,
        Guid? HeroId,
        string? ItemKey,
        int Quantity,
        int PriceGold) : IRequest<Guid>, IPlayerScopedRequest, IIdempotentRequest;

    /// <summary>
    /// Порядок перевірок — від дешевших і зрозуміліших гравцю: ринок відкритий,
    /// є вільний лот, товар придатний, ціна в коридорі, є золото на податок.
    /// Застава й списання податку — в одній транзакції з лотом.
    /// </summary>
    public sealed class ListOnMarketCommandHandler : IRequestHandler<ListOnMarketCommand, Guid>
    {
        private readonly IVillageRepository _villages;
        private readonly IMarketRepository _market;
        private readonly MarketGoods _goods;
        private readonly MarketDesk _desk;
        private readonly GameCatalog _catalog;
        private readonly IServerContext _serverContext;
        private readonly IUnitOfWork _unitOfWork;
        private readonly TimeProvider _timeProvider;
        private readonly ILogger<ListOnMarketCommandHandler> _logger;

        public ListOnMarketCommandHandler(
            IVillageRepository villages,
            IMarketRepository market,
            MarketGoods goods,
            MarketDesk desk,
            GameCatalog catalog,
            IServerContext serverContext,
            IUnitOfWork unitOfWork,
            TimeProvider timeProvider,
            ILogger<ListOnMarketCommandHandler> logger)
        {
            _villages = villages;
            _market = market;
            _goods = goods;
            _desk = desk;
            _catalog = catalog;
            _serverContext = serverContext;
            _unitOfWork = unitOfWork;
            _timeProvider = timeProvider;
            _logger = logger;
        }

        public async Task<Guid> Handle(ListOnMarketCommand request, CancellationToken cancellationToken)
        {
            var now = _timeProvider.GetUtcNow().UtcDateTime;

            var village = await _villages.GetByPlayerIdAsync(request.PlayerId, cancellationToken)
                ?? throw new InvalidOperationException($"Village not found for player {request.PlayerId}.");

            var level = _desk.RequireOpen(village);
            var limit = _desk.ListingLimit(level);

            if (await _market.CountActiveAsync(request.PlayerId, cancellationToken) >= limit)
                throw new RequirementNotMetException(RefusalReasons.MarketListingLimit,
                    $"Player {request.PlayerId} already has {limit} active listings.", limit);

            var goods = await _goods.AppraiseAsync(request.PlayerId, request.Kind, request.EquipmentId, request.HeroId,
                request.ItemKey, request.Quantity, cancellationToken);

            var corridor = await _desk.CorridorAsync(goods.PricingKey, cancellationToken);

            if (!corridor.Allows(request.PriceGold, goods.Units))
                throw new RequirementNotMetException(RefusalReasons.MarketPriceOutOfCorridor,
                    $"Price {request.PriceGold} is outside the corridor for {goods.Units} units of '{goods.PricingKey}'.",
                    corridor.MinPrice(goods.Units), corridor.MaxPrice(goods.Units));

            var tax = _desk.ListingTax(request.PriceGold);

            // Податок списується до застави: гравець без золота не має
            // побачити свій меч знятим із героя заради відмови
            village.ChargeCost([new ResourceCost { Resource = "gold", Amount = tax }], now);

            await _goods.TakeIntoCustodyAsync(request.PlayerId, goods, now, cancellationToken);

            var listing = new MarketListing(Guid.NewGuid(), _serverContext.ServerId, request.PlayerId, goods.Kind,
                goods.EquipmentId, goods.HeroId, goods.ItemKey, goods.Quantity, goods.Units, goods.PricingKey,
                request.PriceGold, tax, now, TimeSpan.FromHours(_catalog.Config.Market.ListingHours));

            await _market.AddAsync(listing, cancellationToken);
            await _unitOfWork.SaveChangesAsync(cancellationToken);

            _logger.LogInformation("Player {PlayerId} listed {Kind} '{ItemKey}' for {Price} gold (tax {Tax})",
                request.PlayerId, goods.Kind, goods.ItemKey, request.PriceGold, tax);

            return listing.Id;
        }
    }
}
