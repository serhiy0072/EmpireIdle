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

        /// <summary>
        /// Рівень світу, на якому пропозиція продається вікном (GDD §6.1: предмет апу тіру N —
        /// лише WindowDays днів після відкриття тіру, далі — інші джерела). null — продається завжди.
        /// </summary>
        public int? ServerLevel { get; set; }

        /// <summary>Довжина вікна в днях від досягнення ServerLevel. Не довша за рівень світу — інакше вікно пережило б його.</summary>
        public int WindowDays { get; set; }

        /// <summary>До якого моменту пропозиція в продажу; null — постійна. Минулий момент — вікно закрите.</summary>
        public DateTime? OnSaleUntil(int serverLevel, DateTime levelSince)
            => ServerLevel is not { } level
                ? null
                : serverLevel == level ? levelSince.AddDays(WindowDays) : DateTime.MinValue;

        /// <summary>Чи продається пропозиція зараз.</summary>
        public bool IsOnSaleAt(int serverLevel, DateTime levelSince, DateTime utcNow)
            => OnSaleUntil(serverLevel, levelSince) is not { } until || utcNow < until;
    }
}
