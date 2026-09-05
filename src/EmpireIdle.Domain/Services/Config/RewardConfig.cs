namespace EmpireIdle.Domain.Services.Config
{
    /// <summary>Одна нагорода. Спільна для квестів, віх, івентів, лутбоксів.</summary>
    public class RewardConfig
    {
        /// <summary>Gems, Resource, Item.</summary>
        public string Type { get; set; } = null!;

        /// <summary>Ключ ресурсу або предмета; для Gems не потрібен.</summary>
        public string? Key { get; set; }

        public int Amount { get; set; }
    }
}
