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
    }
}
