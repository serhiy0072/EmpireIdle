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
        /// <summary>Ремонт зламаної зброї — за gems: база плюс надбавка за кожен рівень заточки.</summary>
        public int RepairGemsBase { get; set; } = 20;

        public int RepairGemsPerLevel { get; set; } = 8;

        /// <summary>Скільки статів артефакт має одразу.</summary>
        public int ArtifactBaseStats { get; set; } = 2;

        /// <summary>Рівні, на яких артефакт отримує новий стат.</summary>
        public List<int> ArtifactStatLevels { get; set; } = [4, 8];

        /// <summary>Рівні, на яких качаються наявні стати.</summary>
        public List<int> ArtifactUpgradeLevels { get; set; } = [12, 16, 20];

        /// <summary>Шанс прокачати два стати замість одного.</summary>
        public double DoubleUpgradeChance { get; set; } = 0.1;

        /// <summary>Пул статів артефактів.</summary>
        public List<ArtifactStatConfig> ArtifactStats { get; set; } = new();

        /// <summary>Бонуси за повні набори артефактів.</summary>
        public List<SetBonusConfig> SetBonuses { get; set; } = new();

        /// <summary>
        /// Множник значень за рідкістю артефакта. Той самий стат на
        /// unique-артефакті вартий більше, ніж на common.
        /// </summary>
        public Dictionary<string, double> ArtifactRarityMultipliers { get; set; } = new();

        /// <summary>Рівні й характер родин наборів артефактів.</summary>
        public List<ArtifactSetConfig> ArtifactSets { get; set; } = new();

        /// <summary>
        /// Множник значень за рівнем набору: артефакт із важчого данжу сильніший.
        /// Діє разом із множником рідкості. Порожньо — множник 1.0.
        /// </summary>
        public List<double> ArtifactTierMultipliers { get; set; } = new();

        /// <summary>У скільки разів характерний стат імовірніший за звичайний при ролі.</summary>
        public double ArtifactFocusWeight { get; set; } = 1.0;

        /// <summary>
        /// Родина набору за SetKey предмета (<c>{Key}_{рідкість}</c>);
        /// null — предмет поза родинами, ролиться без рівня й характеру.
        /// </summary>
        public ArtifactSetConfig? FindArtifactSet(string? setKey)
            => setKey is null
                ? null
                : ArtifactSets.FirstOrDefault(set => Enum.GetNames<Enums.Rarity>()
                    .Any(rarity => setKey == $"{set.Key}_{rarity.ToLowerInvariant()}"));
    }
}
