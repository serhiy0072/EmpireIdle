using EmpireIdle.Domain.Enums;

namespace EmpireIdle.Domain.Services.Config
{
    /// <summary>Спільні правила артефактів: слоти, рівень, майстерність, набори.</summary>
    public class EquipmentConfig
    {
        /// <summary>
        /// Артефактні слоти героя за типом, по одному кожного. Номер слота —
        /// позиція в списку, тож порядок тут — порядок на екрані героя.
        /// </summary>
        public List<ArtifactSlotConfig> ArtifactSlots { get; set; } = new();

        /// <summary>Номер слота для типу артефакта; null — такого типу немає.</summary>
        public int? ArtifactSlotIndex(string? slotKey)
        {
            var index = ArtifactSlots.FindIndex(slot => slot.Key == slotKey);

            return index < 0 ? null : index;
        }

        /// <summary>
        /// Стеля рівня артефакта (GDD §6.4, §9.12): рівень качається згодовуванням іншого
        /// спорядження й гаєчок; на ньому ж — ролли нових статів (ArtifactStatLevels, ArtifactUpgradeLevels).
        /// </summary>
        public int MaxLevel { get; set; } = 20;

        /// <summary>Приріст статів за рівень артефакта, часткою: 0.05 — +5% за рівень.</summary>
        public double LevelBonusPerLevel { get; set; } = 0.05;

        /// <summary>Досвід до рівня n: round10(LevelExperienceBase · n^LevelExperienceExponent).</summary>
        public double LevelExperienceBase { get; set; } = 40;

        public double LevelExperienceExponent { get; set; } = 1.4;

        /// <summary>
        /// Базовий досвід, який дає згодований артефакт за рідкістю, плюс увесь вкладений у нього.
        /// Рідкості без значення (унікальні) згодувати не можна.
        /// </summary>
        public Dictionary<Rarity, int> FeedExperience { get; set; } = new();

        /// <summary>Стеля майстерності коваля — заточки артефакта за золото з шансом.</summary>
        public int MaxMastery { get; set; } = 10;

        /// <summary>Приріст статів за рівень майстерності, часткою; складається з бонусом рівня.</summary>
        public double MasteryBonusPerLevel { get; set; } = 0.10;

        /// <summary>Будівля, де кують — без неї майстерність недоступна.</summary>
        public string ForgeBuildingKey { get; set; } = "forge";

        /// <summary>Ціна першого рівня майстерності в золоті.</summary>
        public int MasteryBaseGold { get; set; } = 500;

        /// <summary>Множник ціни за кожен наступний рівень майстерності.</summary>
        public double MasteryCostGrowth { get; set; } = 1.7;

        /// <summary>
        /// До цього рівня майстерність не провалюється: перші кроки передбачувані,
        /// гравець спершу вчиться механіці, потім ризикує.
        /// </summary>
        public int SafeMasteryLevel { get; set; } = 3;

        /// <summary>Падіння шансу успіху за рівень понад безпечний, часткою; не нижче за MinSuccessChance.</summary>
        public double SuccessDropPerLevel { get; set; } = 0.10;

        /// <summary>Нижня межа шансу успіху. Поломки немає — невдача лише з'їдає золото.</summary>
        public double MinSuccessChance { get; set; } = 0.30;

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
