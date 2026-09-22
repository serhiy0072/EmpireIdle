namespace EmpireIdle.Domain.Services.Config
{
    /// <summary>Предмет у крамниці за gems: есенції еволюції, бусти, ящики.</summary>
    public class ShopItemConfig
    {
        /// <summary>Ключ предмета з Items.</summary>
        public string ItemKey { get; set; } = null!;

        /// <summary>Ціна однієї штуки в gems.</summary>
        public int PriceGems { get; set; }

        /// <summary>Скільки штук можна купити за один запит — стеля проти помилкового кліку на сотню.</summary>
        public int MaxPerPurchase { get; set; } = 10;
    }
}
