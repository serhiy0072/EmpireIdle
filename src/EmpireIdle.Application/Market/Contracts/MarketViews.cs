namespace EmpireIdle.Application.Market.Contracts
{
    /// <summary>
    /// Лот у поданні для клієнта. Назви товарів клієнт бере з каталогу за ItemKey;
    /// деталі екземпляра — лише для свого виду.
    /// </summary>
    /// <param name="Kind">"Equipment", "Hero" або "Item".</param>
    /// <param name="State">"Active", "Sold", "Cancelled" або "Expired".</param>
    /// <param name="Units">Одиниці ціни: Power або кількість штук.</param>
    public record MarketListingView(
        Guid Id,
        string Kind,
        string ItemKey,
        int Quantity,
        int PriceGold,
        double PricePerUnit,
        double Units,
        DateTime ListedAt,
        DateTime ExpiresAt,
        string State,
        bool IsOwn,
        MarketEquipmentView? Equipment,
        MarketHeroView? Hero);

    /// <param name="Slot">"Weapon" або "Artifact".</param>
    public record MarketEquipmentView(string Slot, string Rarity, int EnhancementLevel, IReadOnlyDictionary<string, double> Stats);

    public record MarketHeroView(int Level, int Tier, int Constellation);

    /// <summary>Сторінка вітрини.</summary>
    public record MarketPageView(IReadOnlyList<MarketListingView> Listings, int Total, int Page, int PageSize);

    /// <summary>
    /// Стан ринку для гравця: відкритий чи ні, ліміт і власні лоти.
    /// </summary>
    /// <param name="OpensAtTownHall">Рівень ратуші, з якого ринок відкривається; null — ринку немає в грі.</param>
    public record MyMarketView(
        bool IsOpen,
        int? OpensAtTownHall,
        int ListingLimit,
        int ActiveListings,
        double TaxShare,
        int ListingHours,
        IReadOnlyList<MarketListingView> Listings);

    /// <summary>Котирування для форми виставлення: дозволений діапазон і податок.</summary>
    /// <param name="SuggestedPrice">Середина коридору — розумна ціна за замовчуванням.</param>
    public record MarketQuoteView(double Units, int MinPrice, int MaxPrice, int SuggestedPrice, double TaxShare);
}
