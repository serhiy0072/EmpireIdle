using EmpireIdle.Domain.Enums;

namespace EmpireIdle.Domain.Services.Config
{
    /// <summary>Конфігурація типу предмета.</summary>
    public class ItemConfig
    {
        /// <summary>Унікальний ключ предмета.</summary>
        public string Key { get; set; } = null!;

        /// <summary>Відображувана назва.</summary>
        public string DisplayName { get; set; } = null!;

        /// <summary>Опис для гравця.</summary>
        public string Description { get; set; } = null!;

        /// <summary>Рідкість предмета.</summary>
        public Rarity Rarity { get; set; } = Rarity.Common;

        /// <summary>Тип ефекту: speedup, resources, healing, boost, equipment.</summary>
        public string Type { get; set; } = null!;

        // --- параметри за типами ---

        /// <summary>resources: що і скільки додає.</summary>
        public List<ResourceCost> Resources { get; set; } = new();

        /// <summary>boost: на що діє — production / attack / defense.</summary>
        public string? BoostTarget { get; set; }

        /// <summary>boost: множник (2.0 = подвоєння).</summary>
        public double Multiplier { get; set; } = 1.0;

        /// <summary>boost: скільки годин діє.</summary>
        public int DurationHours { get; set; }

        // --- equipment ---

        /// <summary>equipment: зброя чи артефакт.</summary>
        public EquipmentSlot? Slot { get; set; }

        /// <summary>
        /// equipment: класи героїв, яким зброя підходить. Порожньо — підходить усім.
        /// Пара одноручних рахується одним предметом і заточується разом:
        /// це свідоме спрощення, окремого слота для лівої руки немає.
        /// </summary>
        public List<string> WeaponClasses { get; set; } = new();

        /// <summary>
        /// equipment, лише артефакт: тип слота (ключ з Equipment.ArtifactSlots).
        /// Артефакт сам визначає, куди вдягається, — гравець слот не обирає.
        /// </summary>
        public string? ArtifactSlot { get; set; }

        /// <summary>
        /// Бойова властивість унікального артефакта для данжів. Не росте від
        /// прокачки предмета: це й відрізняє унікальний набір від рідкісного,
        /// де більші лише звичайні стати.
        /// </summary>
        public DungeonStat? UniqueStat { get; set; }

        /// <summary>Сила унікальної властивості часткою: 0.1 — плюс десять відсотків.</summary>
        public double UniqueStatValue { get; set; }

        /// <summary>
        /// equipment: базові стати зброї. В артефактів порожні — їхні стати
        /// випадкові й лежать на екземплярі, а не на типі.
        /// </summary>
        public Dictionary<string, double> BaseStats { get; set; } = new();

        /// <summary>equipment: ключ набору, за повний комплект якого дається бонус.</summary>
        public string? SetKey { get; set; }

        /// <summary>equipment: ціна зброї в золоті; артефакти не продаються.</summary>
        public int PriceGold { get; set; }
    }
}
