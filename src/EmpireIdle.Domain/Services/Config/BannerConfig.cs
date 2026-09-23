using EmpireIdle.Domain.Enums;

namespace EmpireIdle.Domain.Services.Config
{
    /// <summary>
    /// Банер: пул лотів, ціна ролла в gems і пороги pity.
    ///
    /// Предмета-ключа немає: ролл оплачується gems напряму, тож банер —
    /// це не товар в інвентарі, а прайс-лист з пулом.
    /// </summary>
    public class BannerConfig
    {
        /// <summary>Ключ конкретного банера, наприклад "dawn_2026_09".</summary>
        public string Key { get; set; } = null!;

        public string DisplayName { get; set; } = null!;

        /// <summary>
        /// Тип банера. Прогрес pity живе на парі «гравець + група», тому
        /// новий банер того самого типу продовжує накопичене, а не обнуляє його.
        /// </summary>
        public string PityGroup { get; set; } = null!;

        /// <summary>Ціна одного ролла.</summary>
        public int PriceGems { get; set; }

        /// <summary>Ціна ролла в печатках призову; 0 — цей банер за печатки не крутять.</summary>
        public int PriceSeals { get; set; }

        /// <summary>Через скільки роллів без рідкісного він гарантований.</summary>
        public int RarePity { get; set; } = 10;

        /// <summary>Те саме для унікального.</summary>
        public int UniquePity { get; set; } = 50;

        /// <summary>
        /// Банерний лот — той, заради якого банер існує. null означає
        /// звичайний пул без правила 50/50.
        /// </summary>
        public string? FeaturedKey { get; set; }

        /// <summary>Вікно показу. null з обох боків — банер постійний.</summary>
        public DateTimeOffset? StartsAt { get; set; }

        /// <inheritdoc cref="StartsAt"/>
        public DateTimeOffset? EndsAt { get; set; }

        public List<BannerDropConfig> Drops { get; set; } = new();

        /// <summary>Категорія банера: нею обмежений пул гарантій.</summary>
        public BannerKind Kind { get; set; }
    }
}
