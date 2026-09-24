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
        Guid? HeroId,
        string ItemKey,
        int Quantity,
        double Units,
        string PricingKey);

    /// <summary>
    /// Товар ринку в чотирьох станах: оцінка, застава, повернення продавцю
    /// й передача покупцю. Три види товару поводяться по-різному, і без
    /// цього сервісу кожна команда ринку тримала б власну копію розгалуження.
    /// </summary>
    public class MarketGoods
    {
        private readonly IInventoryRepository _inventory;
        private readonly IHeroRepository _heroes;
        private readonly IVillageRepository _villages;
        private readonly IGarrisonRepository _garrisons;
        private readonly ItemGranter _granter;
        private readonly HeroStats _heroStats;
        private readonly GameCatalog _catalog;

        public MarketGoods(
            IInventoryRepository inventory,
            IHeroRepository heroes,
            IVillageRepository villages,
            IGarrisonRepository garrisons,
            ItemGranter granter,
            HeroStats heroStats,
            GameCatalog catalog)
        {
            _inventory = inventory;
            _heroes = heroes;
            _villages = villages;
            _garrisons = garrisons;
            _granter = granter;
            _heroStats = heroStats;
            _catalog = catalog;
        }

        /// <summary>
        /// Оцінка без мутацій: і котирування, і виставлення бачать ту саму
        /// кількість одиниць. Чуже й неіснуюче не розрізняються — 404.
        /// </summary>
        public async Task<MarketGoodsInfo> AppraiseAsync(Guid playerId, MarketListingKind kind, Guid? equipmentId,
            Guid? heroId, string? itemKey, int quantity, CancellationToken cancellationToken)
        {
            switch (kind)
            {
                case MarketListingKind.Equipment:
                {
                    var item = await OwnEquipmentAsync(playerId, equipmentId, cancellationToken);

                    // Зламаний не має сили — ціна за силу втратила б сенс
                    if (item.IsBroken)
                        throw new InvalidStateException(RefusalReasons.EquipmentBroken, $"Equipment {item.Id} is broken.");

                    return new MarketGoodsInfo(kind, item.Id, null, item.ItemKey, 1,
                        _heroStats.Power(item), MarketPricing.CategoryOf(item.Slot));
                }

                case MarketListingKind.Hero:
                {
                    var hero = await OwnHeroAsync(playerId, heroId, cancellationToken);
                    var config = _catalog.FindHero(hero.HeroKey)
                        ?? throw new EntityNotFoundException("Hero config", hero.HeroKey);

                    return new MarketGoodsInfo(kind, null, hero.Id, hero.HeroKey, 1,
                        _heroStats.Power(hero, config), MarketPricing.CategoryOf(config.Rank));
                }

                case MarketListingKind.Item:
                {
                    var key = itemKey ?? throw new EntityNotFoundException("Item", "(none)");
                    var config = _catalog.FindItem(key) ?? throw new EntityNotFoundException("Item", key);

                    if (!config.Tradeable)
                        throw new RequirementNotMetException(RefusalReasons.MarketNotTradeable,
                            $"Item '{key}' is not tradeable.", config.DisplayName);

                    return new MarketGoodsInfo(kind, null, null, key, quantity, quantity, MarketPricing.CategoryOfItem(key));
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

                case MarketListingKind.Hero:
                {
                    var hero = await OwnHeroAsync(playerId, goods.HeroId, cancellationToken);

                    // Лот продається за силу на момент виставлення: прокачка, що
                    // завершиться вже на ринку, продала б покупцю не того героя
                    var order = await _heroes.GetActiveOrderAsync(playerId, cancellationToken);

                    if (order is not null && order.HeroId == hero.Id)
                        throw new RequirementNotMetException(RefusalReasons.MarketHeroTraining, $"Hero {hero.Id} is training.");

                    hero.PutOnMarket(utcNow);
                    break;
                }

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

                case MarketListingKind.Hero:
                {
                    var hero = await ListedHeroAsync(listing, cancellationToken);
                    var (garrisonId, leaderSlotFree) = await HomeAsync(listing.SellerId, cancellationToken);

                    hero.TakeOffMarket(garrisonId, leaderSlotFree, utcNow);
                    break;
                }

                case MarketListingKind.Item:
                    await _granter.GrantAsync(listing.SellerId, listing.ItemKey, listing.Quantity, cancellationToken);
                    break;
            }
        }

        /// <summary>
        /// Продаж: товар переходить покупцю. Героя, який у покупця вже є,
        /// купити не можна — інакше це обхід сузір'я.
        /// </summary>
        public async Task HandOverAsync(MarketListing listing, Guid buyerId, DateTime utcNow,
            TimeSpan resaleCooldown, CancellationToken cancellationToken)
        {
            var lockedUntil = utcNow + resaleCooldown;

            switch (listing.Kind)
            {
                case MarketListingKind.Equipment:
                    (await ListedEquipmentAsync(listing, cancellationToken)).SellTo(buyerId, lockedUntil, utcNow);
                    break;

                case MarketListingKind.Hero:
                {
                    var hero = await ListedHeroAsync(listing, cancellationToken);

                    if (await _heroes.GetByKeyAsync(buyerId, hero.HeroKey, cancellationToken) is not null)
                        throw new RequirementNotMetException(RefusalReasons.MarketHeroAlreadyOwned,
                            $"Player {buyerId} already has hero '{hero.HeroKey}'.",
                            _catalog.FindHero(hero.HeroKey)?.DisplayName ?? hero.HeroKey);

                    var (garrisonId, leaderSlotFree) = await HomeAsync(buyerId, cancellationToken);

                    hero.SellTo(buyerId, garrisonId, leaderSlotFree, lockedUntil, utcNow);
                    break;
                }

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

        private async Task<Hero> OwnHeroAsync(Guid playerId, Guid? heroId, CancellationToken cancellationToken)
        {
            var id = heroId ?? throw new EntityNotFoundException("Hero", "(none)");

            var hero = await _heroes.GetByIdAsync(id, cancellationToken);

            return hero is not null && hero.PlayerId == playerId
                ? hero
                : throw new EntityNotFoundException("Hero", id.ToString());
        }

        /// <summary>Товар активного лота зник — це поломка даних, а не дія гравця.</summary>
        private async Task<EquipmentItem> ListedEquipmentAsync(MarketListing listing, CancellationToken cancellationToken)
            => await _inventory.GetEquipmentByIdAsync(listing.EquipmentId!.Value, cancellationToken)
                ?? throw new InvalidOperationException($"Listing {listing.Id} lost its equipment {listing.EquipmentId}.");

        private async Task<Hero> ListedHeroAsync(MarketListing listing, CancellationToken cancellationToken)
            => await _heroes.GetByIdAsync(listing.HeroId!.Value, cancellationToken)
                ?? throw new InvalidOperationException($"Listing {listing.Id} lost its hero {listing.HeroId}.");

        /// <summary>Гарнізон рідного села гравця і чи вільне в ньому місце лідера.</summary>
        private async Task<(Guid GarrisonId, bool LeaderSlotFree)> HomeAsync(Guid playerId, CancellationToken cancellationToken)
        {
            var village = await _villages.GetByPlayerIdAsync(playerId, cancellationToken)
                ?? throw new InvalidOperationException($"Village not found for player {playerId}.");

            var garrison = await _garrisons.GetByVillageIdAsync(village.Id, cancellationToken)
                ?? throw new InvalidOperationException($"Garrison not found for village {village.Id}.");

            var leader = await _heroes.GetLeaderAsync(garrison.Id, playerId, cancellationToken);

            return (garrison.Id, leader is null);
        }
    }
}
