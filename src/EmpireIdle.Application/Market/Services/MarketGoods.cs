using EmpireIdle.Application.Common.Services;
using EmpireIdle.Application.Interfaces;
using EmpireIdle.Domain.Entities;
using EmpireIdle.Domain.Enums;
using EmpireIdle.Domain.Exceptions;
using EmpireIdle.Domain.Services;

namespace EmpireIdle.Application.Market.Services
{
    /// <summary>
    /// Що саме продається: вид, екземпляр, скільки одиниць для ціни й у якій
    /// категорії рахується медіана.
    /// </summary>
    public sealed record MarketGoodsInfo(
        MarketListingKind Kind,
        Guid? EquipmentId,
        string ItemKey,
        int Quantity,
        double Units,
        string PricingKey);

    /// <summary>
    /// Товар ринку в чотирьох станах: оцінка, застава, повернення продавцю
    /// й передача покупцю. Два види товару поводяться по-різному, і без
    /// цього сервісу кожна команда ринку тримала б власну копію розгалуження.
    /// </summary>
    public class MarketGoods
    {
        private readonly IInventoryRepository _inventory;
        private readonly ItemGranter _granter;
        private readonly HeroStats _heroStats;
        private readonly GameCatalog _catalog;

        public MarketGoods(
            IInventoryRepository inventory,
            ItemGranter granter,
            HeroStats heroStats,
            GameCatalog catalog)
        {
            _inventory = inventory;
            _granter = granter;
            _heroStats = heroStats;
            _catalog = catalog;
        }

        /// <summary>
        /// Оцінка без мутацій: і котирування, і виставлення бачать ту саму
        /// кількість одиниць. Чуже й неіснуюче не розрізняються — 404.
        /// </summary>
        public async Task<MarketGoodsInfo> AppraiseAsync(Guid playerId, MarketListingKind kind, Guid? equipmentId,
            string? itemKey, int quantity, CancellationToken cancellationToken)
        {
            switch (kind)
            {
                case MarketListingKind.Equipment:
                {
                    var item = await OwnEquipmentAsync(playerId, equipmentId, cancellationToken);

                    // Зламаний не має сили — ціна за силу втратила б сенс
                    if (item.IsBroken)
                        throw new InvalidStateException(RefusalReasons.EquipmentBroken, $"Equipment {item.Id} is broken.");

                    return new MarketGoodsInfo(kind, item.Id, item.ItemKey, 1,
                        _heroStats.Power(item), MarketPricing.CategoryOf(item.Slot));
                }

                case MarketListingKind.Item:
                {
                    var key = itemKey ?? throw new EntityNotFoundException("Item", "(none)");
                    var config = _catalog.FindItem(key) ?? throw new EntityNotFoundException("Item", key);

                    if (!config.Tradeable)
                        throw new RequirementNotMetException(RefusalReasons.MarketNotTradeable,
                            $"Item '{key}' is not tradeable.", config.DisplayName);

                    return new MarketGoodsInfo(kind, null, key, quantity, quantity, MarketPricing.CategoryOfItem(key));
                }

                default:
                    throw new ArgumentOutOfRangeException(nameof(kind), kind, "Unknown listing kind.");
            }
        }

        /// <summary>Товар переходить у заставу ринку: його більше не можна використати.</summary>
        public async Task TakeIntoCustodyAsync(Guid playerId, MarketGoodsInfo goods, DateTime utcNow,
            CancellationToken cancellationToken)
        {
            switch (goods.Kind)
            {
                case MarketListingKind.Equipment:
                    (await OwnEquipmentAsync(playerId, goods.EquipmentId, cancellationToken)).PutOnMarket(utcNow);
                    break;

                case MarketListingKind.Item:
                {
                    var stack = await _inventory.GetItemAsync(playerId, goods.ItemKey, cancellationToken);
                    var have = stack?.Count ?? 0;

                    if (stack is null || have < goods.Quantity)
                        throw new RequirementNotMetException(RefusalReasons.MarketNotEnoughItems,
                            $"Player {playerId} has {have} of '{goods.ItemKey}', {goods.Quantity} needed.",
                            _catalog.FindItem(goods.ItemKey)?.DisplayName ?? goods.ItemKey, goods.Quantity, have);

                    stack.Consume(goods.Quantity);
                    break;
                }
            }
        }

        /// <summary>Лот знято або строк минув — товар повертається продавцю.</summary>
        public async Task ReturnToSellerAsync(MarketListing listing, DateTime utcNow, CancellationToken cancellationToken)
        {
            switch (listing.Kind)
            {
                case MarketListingKind.Equipment:
                    (await ListedEquipmentAsync(listing, cancellationToken)).TakeOffMarket(utcNow);
                    break;

                case MarketListingKind.Item:
                    await _granter.GrantAsync(listing.SellerId, listing.ItemKey, listing.Quantity, cancellationToken);
                    break;
            }
        }

        /// <summary>Продаж: товар переходить покупцю.</summary>
        public async Task HandOverAsync(MarketListing listing, Guid buyerId, DateTime utcNow,
            TimeSpan resaleCooldown, CancellationToken cancellationToken)
        {
            var lockedUntil = utcNow + resaleCooldown;

            switch (listing.Kind)
            {
                case MarketListingKind.Equipment:
                    (await ListedEquipmentAsync(listing, cancellationToken)).SellTo(buyerId, lockedUntil, utcNow);
                    break;

                case MarketListingKind.Item:
                    await _granter.GrantAsync(buyerId, listing.ItemKey, listing.Quantity, cancellationToken);
                    break;
            }
        }

        private async Task<EquipmentItem> OwnEquipmentAsync(Guid playerId, Guid? equipmentId, CancellationToken cancellationToken)
        {
            var id = equipmentId ?? throw new EntityNotFoundException("Equipment", "(none)");

            var item = await _inventory.GetEquipmentByIdAsync(id, cancellationToken);

            return item is not null && item.PlayerId == playerId
                ? item
                : throw new EntityNotFoundException("Equipment", id.ToString());
        }

        /// <summary>Товар активного лота зник — це поломка даних, а не дія гравця.</summary>
        private async Task<EquipmentItem> ListedEquipmentAsync(MarketListing listing, CancellationToken cancellationToken)
            => await _inventory.GetEquipmentByIdAsync(listing.EquipmentId!.Value, cancellationToken)
                ?? throw new InvalidOperationException($"Listing {listing.Id} lost its equipment {listing.EquipmentId}.");
    }
}
