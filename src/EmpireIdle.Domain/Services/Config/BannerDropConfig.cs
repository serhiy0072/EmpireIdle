using EmpireIdle.Domain.Enums;

namespace EmpireIdle.Domain.Services.Config
{
    /// <summary>
    /// Один лот банера. Видається тим самим диспетчером, що й нагорода квесту:
    /// інакше гача мала б власний шлях видачі й власні помилки.
    /// </summary>
    public class BannerDropConfig
    {
        /// <summary>Ключ лота, унікальний у межах банера. Іде в журнал роллів і в таблицю шансів.</summary>
        public string Key { get; set; } = null!;

        public string DisplayName { get; set; } = null!;

        /// <summary>Рідкість лота. Саме вона рухає обидва лічильники pity.</summary>
        public Rarity Rarity { get; set; }

        /// <summary>Відносна вага випадіння.</summary>
        public int Weight { get; set; }

        /// <summary>Що саме отримує гравець.</summary>
        public List<RewardConfig> Rewards { get; set; } = new();

        /// <summary>
        /// Категорія лота. null — звичайний лут: він випадає на будь-якому банері,
        /// не входить у пул гарантій і не рухає лічильники.
        /// </summary>
        public BannerKind? Kind { get; set; }
    }
}
