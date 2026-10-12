namespace EmpireIdle.Domain.Services.Config
{
    /// <summary>
    /// Позиція таблиці скрині — звичайна нагорода з вагою. Крім типів нагород диспетчера,
    /// приймає ще два:
    /// <list type="bullet">
    /// <item>"Lootbox" — вкладена скриня: Key — її ключ, Amount — скільки разів відкрити одразу;</item>
    /// <item>"Artifact" — випадковий артефакт: Key — рідкість, набір — з данжів, відкритих гравцеві;</item>
    /// <item>"RandomHero" — випадковий цілий герой: Key — рідкість, Amount — скільки героїв (дублікат стає уламками);</item>
    /// <item>"RandomHeroShards" — уламки випадкового героя: Key — рідкість, Amount — скільки уламків.</item>
    /// </list>
    /// </summary>
    public class LootboxEntryConfig : RewardConfig
    {
        /// <summary>Тип вкладеної скрині.</summary>
        public const string LootboxType = "Lootbox";

        /// <summary>Тип випадкового артефакта заданої рідкості.</summary>
        public const string ArtifactType = "Artifact";

        /// <summary>Тип випадкового цілого героя заданої рідкості.</summary>
        public const string RandomHeroType = "RandomHero";

        /// <summary>Тип уламків випадкового героя заданої рідкості.</summary>
        public const string RandomHeroShardsType = "RandomHeroShards";

        /// <summary>Вага: шанс позиції — вага / сума ваг таблиці.</summary>
        public int Weight { get; set; }

        /// <summary>Позиція гарантії: на відкритті PityWithin випадає одна з таких, за їхніми вагами.</summary>
        public bool Pity { get; set; }

        /// <summary>Вкладена скриня. Регістр типу ігнорується — як у диспетчері нагород.</summary>
        public bool IsLootbox => string.Equals(Type, LootboxType, StringComparison.OrdinalIgnoreCase);

        /// <summary>Випадковий артефакт.</summary>
        public bool IsArtifact => string.Equals(Type, ArtifactType, StringComparison.OrdinalIgnoreCase);

        /// <summary>Випадковий цілий герой.</summary>
        public bool IsRandomHero => string.Equals(Type, RandomHeroType, StringComparison.OrdinalIgnoreCase);

        /// <summary>Уламки випадкового героя.</summary>
        public bool IsRandomHeroShards => string.Equals(Type, RandomHeroShardsType, StringComparison.OrdinalIgnoreCase);
    }
}
