namespace EmpireIdle.Domain.Services.Config
{
    /// <summary>
    /// Позиція таблиці скрині — звичайна нагорода з вагою. Крім типів нагород диспетчера,
    /// приймає ще два:
    /// <list type="bullet">
    /// <item>"Lootbox" — вкладена скриня: Key — її ключ, Amount — скільки разів відкрити одразу;</item>
    /// <item>"Artifact" — випадковий артефакт: Key — рідкість, набір — з данжів, відкритих гравцеві.</item>
    /// </list>
    /// </summary>
    public class LootboxEntryConfig : RewardConfig
    {
        /// <summary>Тип вкладеної скрині.</summary>
        public const string LootboxType = "Lootbox";

        /// <summary>Тип випадкового артефакта заданої рідкості.</summary>
        public const string ArtifactType = "Artifact";

        /// <summary>Вага: шанс позиції — вага / сума ваг таблиці.</summary>
        public int Weight { get; set; }

        /// <summary>Позиція гарантії: на відкритті PityWithin випадає одна з таких, за їхніми вагами.</summary>
        public bool Pity { get; set; }

        /// <summary>Вкладена скриня. Регістр типу ігнорується — як у диспетчері нагород.</summary>
        public bool IsLootbox => string.Equals(Type, LootboxType, StringComparison.OrdinalIgnoreCase);

        /// <summary>Випадковий артефакт.</summary>
        public bool IsArtifact => string.Equals(Type, ArtifactType, StringComparison.OrdinalIgnoreCase);
    }
}
