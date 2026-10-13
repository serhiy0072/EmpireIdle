namespace EmpireIdle.Domain.Services.Config
{
    /// <summary>
    /// Навчальний табір (GDD §6.1, рішення 07.10.2026): герой у слоті отримує рівень опорної п'ятірки
    /// без витрати досвіду. Числа — заглушки до Режисера.
    /// </summary>
    public class TrainingCampConfig
    {
        /// <summary>Скільки найсильніших героїв поза табором задають його рівень — «опорна п'ятірка».</summary>
        public int ReferenceSize { get; set; } = 5;

        /// <summary>
        /// Безкоштовні слоти: рівень ратуші, з якого відкривається кожен. Не спадає —
        /// наступний слот не відкривається раніше за попередній.
        /// </summary>
        public List<int> FreeSlotTownHallLevels { get; set; } = new();

        /// <summary>Слоти понад безкоштовні: ціна кожного наступного в gems.</summary>
        public List<int> ExtraSlotPricesGems { get; set; } = new();

        /// <summary>Скільки годин перезаряджається слот після того, як героя вийняли.</summary>
        public double SlotCooldownHours { get; set; } = 24;

        /// <summary>Ціна в gems, щоб звільнений слот став доступним одразу.</summary>
        public int SkipCooldownGems { get; set; } = 50;
    }
}
