namespace EmpireIdle.Domain.Services
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

        /// <summary>common / rare / legendary.</summary>
        public string Rarity { get; set; } = "common";

        /// <summary>Тип ефекту: speedup, resources, healing, boost, equipment.</summary>
        public string Type { get; set; } = null!;

        /// <summary>
        /// Чи складаються екземпляри в один стек.
        /// Розхідники — так; спорядження з унікальними статами — ні.
        /// </summary>
        public bool IsStackable { get; set; } = true;

        // --- параметри за типами ---

        /// <summary>resources: що і скільки додає.</summary>
        public List<ResourceCost> Resources { get; set; } = new();

        /// <summary>boost: на що діє — production / attack / defense.</summary>
        public string? BoostTarget { get; set; }

        /// <summary>boost: множник (2.0 = подвоєння).</summary>
        public double Multiplier { get; set; } = 1.0;

        /// <summary>boost: скільки годин діє.</summary>
        public int DurationHours { get; set; }
    }
}