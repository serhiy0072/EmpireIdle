namespace EmpireIdle.Domain.Services.Config
{
    /// <summary>
    /// Скриня (GDD §9.12): зважена таблиця нагород із гарантією. Сама скриня — предмет Items
    /// з Type "lootbox" і тим самим ключем, тож лежить у рюкзаку й продається крамницею.
    /// Нова скриня — лише конфіг: таблиця, ваги, гарантія й вкладені скрині.
    /// </summary>
    public class LootboxConfig
    {
        /// <summary>Ключ предмета-скрині з Items.</summary>
        public string Key { get; set; } = null!;

        /// <summary>Позиції таблиці: одна випадає за відкриття, з шансом вага / сума ваг.</summary>
        public List<LootboxEntryConfig> Entries { get; set; } = new();

        /// <summary>
        /// Гарантія: позиція з Pity випадає не пізніше цього відкриття поспіль, лічильник
        /// скидається, щойно вона випала. 0 — скриня без гарантії.
        /// </summary>
        public int PityWithin { get; set; }
    }
}
