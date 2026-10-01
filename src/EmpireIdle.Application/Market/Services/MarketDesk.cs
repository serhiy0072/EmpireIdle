using EmpireIdle.Application.Interfaces;
using EmpireIdle.Domain.Entities;
using EmpireIdle.Domain.Exceptions;
using EmpireIdle.Domain.Services;

namespace EmpireIdle.Application.Market.Services
{
    /// <summary>
    /// Прилавок ринку: чи відкритий він гравцю, скільки лотів дозволено
    /// й у якому коридорі можна ставити ціну. Спільне для виставлення,
    /// котирування й екрана «мої лоти», щоб вони не розходились у правилах.
    /// </summary>
    public class MarketDesk
    {
        private readonly IMarketRepository _market;
        private readonly MarketPricing _pricing;
        private readonly VillageStatus _status;
        private readonly GameCatalog _catalog;

        public MarketDesk(IMarketRepository market, MarketPricing pricing, VillageStatus status, GameCatalog catalog)
        {
            _market = market;
            _pricing = pricing;
            _status = status;
            _catalog = catalog;
        }

        /// <summary>Ключ будівлі ринку; null — ринку в цій грі немає.</summary>
        private string? BuildingKey => _catalog.Config.Market.BuildingKey;

        /// <summary>Рівень ратуші, з якого відкривається ринок; null — ринку немає зовсім.</summary>
        public int? OpensAtTownHall
            => BuildingKey is { } key ? _catalog.Building(key).RequiresMainBuildingLevel : null;

        /// <summary>Чи відкритий ринок гравцю: будівля є й уже не під туманом.</summary>
        public bool IsOpen(Village village)
            => BuildingKey is { } key
               && _status.IsUnlocked(village, key)
               && village.Buildings.Any(b => b.Type == key);

        /// <summary>Те саме, але закритий ринок — відмова з рівнем ратуші, що його відкриє.</summary>
        public void RequireOpen(Village village)
        {
            if (!IsOpen(village))
                throw new RequirementNotMetException(RefusalReasons.MarketLocked,
                    "The market is not open for this village yet.", OpensAtTownHall ?? 0);
        }

        /// <summary>Скільки лотів гравець тримає одночасно — фіксовано, ринок рівнів не має (GDD §3.1).</summary>
        public int ListingLimit => _catalog.Config.Market.ListingLimit;

        /// <summary>
        /// Коридор категорії за останнім знімком медіани. Якір є в кожної
        /// категорії, що може потрапити на ринок, — це стежить валідатор конфіга.
        /// </summary>
        public async Task<PriceCorridor> CorridorAsync(string pricingKey, CancellationToken cancellationToken)
        {
            var snapshot = await _market.GetSnapshotAsync(pricingKey, cancellationToken);

            return _pricing.Corridor(pricingKey, snapshot?.MedianPerUnit)
                ?? throw new InvalidOperationException($"Market has no price anchor for '{pricingKey}'.");
        }

        public int ListingTax(int price) => _pricing.ListingTax(price);
    }
}
