namespace EmpireIdle.Domain.Enums
{
    /// <summary>Стан лота. Із будь-якого стану, крім Active, лот уже не змінюється.</summary>
    public enum MarketListingState
    {
        Active = 1,

        /// <summary>Куплено: предмет у покупця, золото в продавця.</summary>
        Sold = 2,

        /// <summary>Продавець зняв лот; предмет повернувся, податок — ні.</summary>
        Cancelled = 3,

        /// <summary>Строк минув без покупки; предмет повернувся, податок — ні.</summary>
        Expired = 4
    }
}
