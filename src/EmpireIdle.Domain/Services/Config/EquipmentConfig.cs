using EmpireIdle.Domain.Enums;

namespace EmpireIdle.Domain.Services.Config
{
    /// <summary>
    /// Спільні правила артефактів (GDD §9.12): слоти, база, рівень, заточка, набори.
    ///
    /// Два виміри предмета: рівень 0–80 росте пласкою базою, заточка +0…+20 дає відсоткові
    /// бонуси. База не зберігається на предметі, а рахується з цих таблиць — зміна чисел
    /// балансу не вимагає міграції.
    /// </summary>
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

        /// <summary>Тип слота за ключем; null — такого типу немає.</summary>
        public ArtifactSlotConfig? FindArtifactSlot(string? slotKey)
            => ArtifactSlots.FirstOrDefault(slot => slot.Key == slotKey);

        /// <summary>Стеля рівня артефакта; рівень качається згодовуванням спорядження й гаєчок.</summary>
        public int MaxLevel { get; set; } = 80;

        /// <summary>
        /// Досвід на крок до рівня n: round10(Base + Coefficient · (n − 1)^Exponent).
        /// Стала частина робить перші рівні відчутними, степенева — розтягує пізні.
        /// </summary>
        public double LevelExperienceBase { get; set; } = 100;

        public double LevelExperienceCoefficient { get; set; } = 2.2;

        public double LevelExperienceExponent { get; set; } = 1.55;

        /// <summary>
        /// Базовий досвід, який дає згодований артефакт за рідкістю, плюс увесь вкладений у нього.
        /// Рідкості без значення згодувати не можна.
        /// </summary>
        public Dictionary<Rarity, int> FeedExperience { get; set; } = new();

        /// <summary>
        /// Пласка база за рідкістю: стартові числа й приріст за рівень. Однакова для всіх
        /// типів слотів — тип впливає лише на бонуси заточки.
        /// </summary>
        public Dictionary<Rarity, ArtifactBaseConfig> ArtifactBase { get; set; } = new();

        /// <summary>
        /// Множник бази за класом героя, що носить предмет: ключ — клас із HeroesConfig.Classes.
        /// Діє і на стати героя, і на стати юнітів. Клас без запису — множник 1.
        /// </summary>
        public Dictionary<string, ArtifactClassMultiplierConfig> ArtifactClassMultipliers { get; set; } = new();

        /// <summary>Сила за одиницю пласкої атаки й захисту героя.</summary>
        public double HeroStatPower { get; set; } = 1.0;

        /// <summary>
        /// Сила за одиницю пласкої атаки й захисту юнітів. Їхні числа вчетверо більші, бо
        /// додаються до всього загону, — вага зрівнює їх із героївськими.
        /// </summary>
        public double UnitStatPower { get; set; } = 0.25;

        /// <summary>Бонуси заточки: ступені й сила кожного стату.</summary>
        public List<ArtifactBonusConfig> ArtifactBonuses { get; set; } = new();

        /// <summary>Бонус заточки за ключем стату; null — невідомий.</summary>
        public ArtifactBonusConfig? FindArtifactBonus(string stat)
            => ArtifactBonuses.FirstOrDefault(b => b.Stat == stat);

        /// <summary>
        /// Шанси ступенів 1–4 одного рангу заточки, у порядку ступенів: 0.4, 0.3, 0.2, 0.1.
        /// Сума — 1.
        /// </summary>
        public List<double> BonusStepChances { get; set; } = new();

        /// <summary>Стеля заточки: по 5 рангів на кожен із чотирьох бонусів.</summary>
        public int MaxMastery { get; set; } = 20;

        /// <summary>Будівля, де кують — без неї заточка недоступна.</summary>
        public string ForgeBuildingKey { get; set; } = "forge";

        /// <summary>Ціна спроби на +1 у золоті; далі — round10(база · ріст^поточна заточка).</summary>
        public int MasteryBaseGold { get; set; } = 400;

        /// <summary>Множник ціни за кожен наступний рівень заточки.</summary>
        public double MasteryCostGrowth { get; set; } = 1.33;

        /// <summary>
        /// Шанс успіху спроби за поточною заточкою: індекс 0 — спроба на +1.
        /// Явний список, а не формула: так його читає й балансує Режисер.
        /// </summary>
        public List<double> MasterySuccessChances { get; set; } = new();

        /// <summary>
        /// Шанс поломки, якщо спроба з поточної заточки невдала: індекс 0 — спроба на +1.
        /// Кидається лише при невдачі — успіх ніколи не ламає.
        /// </summary>
        public List<double> MasteryBreakChances { get; set; } = new();

        /// <summary>Частка статів і сили, яку дає зламаний предмет, доки його не відремонтують.</summary>
        public double BrokenStatShare { get; set; } = 0.5;

        /// <summary>Ремонт за gems: стільки за кожен рівень заточки зламаного предмета.</summary>
        public int RepairGemsPerMastery { get; set; } = 50;

        /// <summary>Ремкомплект — ремонт без gems на будь-якій заточці; ключ предмета з Items.</summary>
        public string RepairKitItemKey { get; set; } = "repair_kit";

        /// <summary>Бонуси за повні набори артефактів.</summary>
        public List<SetBonusConfig> SetBonuses { get; set; } = new();

        /// <summary>Рівні й характер родин наборів артефактів.</summary>
        public List<ArtifactSetConfig> ArtifactSets { get; set; } = new();

        /// <summary>
        /// Множник бази за рівнем набору: артефакт із важчого данжу сильніший.
        /// Порожньо — множник 1.0.
        /// </summary>
        public List<double> ArtifactTierMultipliers { get; set; } = new();

        /// <summary>
        /// Родина набору за SetKey предмета (<c>{Key}_{рідкість}</c>);
        /// null — предмет поза родинами, без множника рівня.
        /// </summary>
        public ArtifactSetConfig? FindArtifactSet(string? setKey)
            => setKey is null
                ? null
                : ArtifactSets.FirstOrDefault(set => Enum.GetNames<Enums.Rarity>()
                    .Any(rarity => setKey == $"{set.Key}_{rarity.ToLowerInvariant()}"));

        /// <summary>Рівень набору поза списком множників бере останній відомий — як і тір героя.</summary>
        public double TierMultiplier(string? setKey)
        {
            var set = FindArtifactSet(setKey);

            if (set is null || ArtifactTierMultipliers.Count == 0)
                return 1.0;

            return ArtifactTierMultipliers[Math.Clamp(set.Tier - 1, 0, ArtifactTierMultipliers.Count - 1)];
        }
    }
}
