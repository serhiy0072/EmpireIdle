using EmpireIdle.Application.Interfaces;
using EmpireIdle.Application.Market.Contracts;
using EmpireIdle.Domain.Entities;
using EmpireIdle.Domain.Enums;
using EmpireIdle.Domain.Services;

namespace EmpireIdle.Application.Market.Services
{
    /// <summary>
    /// Лоти у view для клієнта. Деталі екземплярів (стати предмета) тягнуться
    /// одним запитом на сторінку, а не одним на лот.
    /// Стати — із заточкою, як в інвентарі: покупець має бачити ті самі
    /// числа, з якими предмет піде в бій.
    /// </summary>
    public class MarketListingProjection
    {
        private readonly IInventoryRepository _inventory;
        private readonly ArtifactStats _artifacts;

        public MarketListingProjection(IInventoryRepository inventory, GameCatalog catalog)
        {
            _inventory = inventory;
            _artifacts = new ArtifactStats(catalog);
        }

        public async Task<List<MarketListingView>> ProjectAsync(IReadOnlyCollection<MarketListing> listings, Guid viewerId,
            CancellationToken cancellationToken)
        {
            var equipmentIds = listings.Where(l => l.EquipmentId is not null).Select(l => l.EquipmentId!.Value).ToList();

            var equipment = equipmentIds.Count == 0
                ? new Dictionary<Guid, EquipmentItem>()
                : (await _inventory.GetEquipmentByIdsReadOnlyAsync(equipmentIds, cancellationToken)).ToDictionary(e => e.Id);

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
                        ? new MarketEquipmentView(item.Slot.ToString(), item.Rarity.ToString(), item.Level, item.Mastery, item.IsBroken,
                            new Dictionary<string, double>(_artifacts.Compute(item)))
                        : null))
                .ToList();
        }
    }
}
