using EmpireIdle.Domain.Enums;

namespace EmpireIdle.Domain.Services.Config
{
    /// <summary>Один можливий предмет із лутбокса.</summary>
    public class LootDropConfig
    {
        public string Key { get; set; } = null!;
        public string DisplayName { get; set; } = null!;

        /// <summary>Рідкість — для pity й відображення.</summary>
        public Rarity Rarity { get; set; }

        /// <summary>Відносна вага випадіння.</summary>
        public int Weight { get; set; }
    }
}
