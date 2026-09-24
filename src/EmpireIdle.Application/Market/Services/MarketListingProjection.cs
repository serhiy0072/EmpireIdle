using EmpireIdle.Application.Interfaces;
using EmpireIdle.Application.Market.Contracts;
using EmpireIdle.Domain.Entities;
using EmpireIdle.Domain.Enums;

namespace EmpireIdle.Application.Market.Services
{
    /// <summary>
    /// Лоти у view для клієнта. Деталі екземплярів (стати предмета, рівень
    /// героя) тягнуться двома запитами на сторінку, а не одним на лот.
    /// </summary>
    public class MarketListingProjection
    {
        private readonly IInventoryRepository _inventory;
        private readonly IHeroRepository _heroes;

        public MarketListingProjection(IInventoryRepository inventory, IHeroRepository heroes)
        {
            _inventory = inventory;
            _heroes = heroes;
        }

        public async Task<List<MarketListingView>> ProjectAsync(IReadOnlyCollection<MarketListing> listings, Guid viewerId,
            CancellationToken cancellationToken)
        {
            var equipmentIds = listings.Where(l => l.EquipmentId is not null).Select(l => l.EquipmentId!.Value).ToList();
            var heroIds = listings.Where(l => l.HeroId is not null).Select(l => l.HeroId!.Value).ToList();

            var equipment = equipmentIds.Count == 0
                ? new Dictionary<Guid, EquipmentItem>()
                : (await _inventory.GetEquipmentByIdsReadOnlyAsync(equipmentIds, cancellationToken)).ToDictionary(e => e.Id);

            var heroes = heroIds.Count == 0
                ? new Dictionary<Guid, Hero>()
                : (await _heroes.GetByIdsReadOnlyAsync(heroIds, cancellationToken)).ToDictionary(h => h.Id);

            return listings
                .Select(listing => new MarketListingView(
                    listing.Id,
                    listing.Kind.ToString(),
                    listing.ItemKey,
                    listing.Quantity,
                    listing.PriceGold,
                    listing.PricePerUnit,
                    listing.Units,
                    listing.ListedAt,
                    listing.ExpiresAt,
                    listing.State.ToString(),
                    listing.SellerId == viewerId,
                    listing.Kind == MarketListingKind.Equipment
                        && equipment.TryGetValue(listing.EquipmentId!.Value, out var item)
                        ? new MarketEquipmentView(item.Slot.ToString(), item.Rarity.ToString(), item.EnhancementLevel,
                            item.Stats.ToDictionary(s => s.StatKey, s => s.Value))
                        : null,
                    listing.Kind == MarketListingKind.Hero
                        && heroes.TryGetValue(listing.HeroId!.Value, out var hero)
                        ? new MarketHeroView(hero.Level, hero.Tier, hero.Constellation)
                        : null))
                .ToList();
        }
    }
}
