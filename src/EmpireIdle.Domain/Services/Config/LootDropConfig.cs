namespace EmpireIdle.Domain.Services.Config
{
    /// <summary>Один можливий предмет із лутбокса.</summary>
    public class LootDropConfig
    {
        public string Key { get; set; } = null!;
        public string DisplayName { get; set; } = null!;

        /// <summary>common / rare / legendary — для pity й відображення.</summary>
        public string Rarity { get; set; } = null!;

        /// <summary>Відносна вага випадіння.</summary>
        public int Weight { get; set; }
    }
}
