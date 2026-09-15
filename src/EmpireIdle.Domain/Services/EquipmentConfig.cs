namespace EmpireIdle.Domain.Services.Config
{
    /// <summary>Спільні правила спорядження: слоти, заточка, набори.</summary>
    public class EquipmentConfig
    {
        /// <summary>Скільки артефактів носить герой.</summary>
        public int ArtifactSlots { get; set; } = 4;

        /// <summary>Стеля заточки, однакова для зброї й артефактів.</summary>
        public int MaxEnhancement { get; set; } = 20;

        /// <summary>Приріст стата за рівень заточки, часткою.</summary>
        public double EnhancementBonusPerLevel { get; set; } = 0.1;

        /// <summary>Будівля, де кують і лагодять зброю.</summary>
        public string ForgeBuildingKey { get; set; } = "forge";

        /// <summary>Вартість першого рівня заточки в золоті.</summary>
        public int EnhanceBaseGold { get; set; } = 200;

        /// <summary>Множник вартості за кожен наступний рівень.</summary>
        public double EnhanceCostGrowth { get; set; } = 1.35;

        /// <summary>
        /// До цього рівня заточка не провалюється. Перші кроки мають бути
        /// передбачувані: гравець спершу вчиться механіці, потім ризикує.
        /// </summary>
        public int SafeEnhancementLevel { get; set; } = 5;

        /// <summary>
        /// Падіння шансу успіху за рівень понад безпечний, часткою.
        /// На двадцятому рівні шанс не опускається нижче за MinSuccessChance.
        /// </summary>
        public double SuccessDropPerLevel { get; set; } = 0.05;

        /// <summary>Нижня межа шансу успіху.</summary>
        public double MinSuccessChance { get; set; } = 0.25;

        /// <summary>
        /// Шанс зламати зброю при невдалій спробі. Невдача сама по собі
        /// не ламає: здебільшого гравець просто втрачає золото.
        /// </summary>
        public double BreakChanceOnFailure { get; set; } = 0.2;

        /// <summary>Вартість ремонту як частка від вартості поточного рівня заточки.</summary>
        public double RepairCostShare { get; set; } = 0.5;
    }
}
