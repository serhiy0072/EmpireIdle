using EmpireIdle.Domain.Enums;

namespace EmpireIdle.Domain.Services.Config
{
    /// <summary>
    /// Звірі (GDD §5.10): правила приручення й перелік типів.
    /// Усі числа — заглушки до Режисера.
    /// </summary>
    public class BeastsConfig
    {
        /// <summary>Скільки додає до шансу приручення кожен рівень звіринця.</summary>
        public double TameChancePerPenLevel { get; set; }

        /// <summary>Стеля шансу як множник до базового: рівень звіринця не робить приручення гарантованим.</summary>
        public double MaxTameChanceMultiplier { get; set; } = 1.0;

        /// <summary>
        /// Гарантія: після стількох перемог поспіль без звіра наступна перемога його дає.
        /// Лічильник окремий на кожен тип.
        /// </summary>
        public int PityWins { get; set; }

        /// <summary>Найвищий ранг звіра; дублікат понад нього — звичайна здобич.</summary>
        public int MaxRank { get; set; } = 1;

        /// <summary>
        /// Ранг — стеля рівня (як тір у героїв): рівень звіра не вище ранг × LevelsPerRank.
        /// Корм веде до стелі, дублікат її піднімає.
        /// </summary>
        public int LevelsPerRank { get; set; } = 1;

        /// <summary>Предмет-корм: окремий стаковий предмет, не їжа зі складу (GDD §5.10).</summary>
        public string FeedItemKey { get; set; } = string.Empty;

        /// <summary>Скільки корму треба з 1 на 2 рівень; одиниця корму — одиниця досвіду.</summary>
        public int BaseExperience { get; set; }

        /// <summary>У скільки разів дорожчає кожен наступний рівень.</summary>
        public double ExperienceGrowth { get; set; } = 1.0;

        public List<BeastConfig> Types { get; set; } = new();
    }

    /// <summary>Тип звіра. Приручається з монстра одного типу — і лише з нього.</summary>
    public class BeastConfig
    {
        public string Key { get; set; } = null!;

        public string DisplayName { get; set; } = null!;

        /// <summary>Тип монстра, з якого приручається звір (GDD §5.10: тип монстра = тип звіра).</summary>
        public string MonsterKey { get; set; } = null!;

        /// <summary>Базовий шанс приручення за перемогу, (0; 1].</summary>
        public double TameChance { get; set; }

        /// <summary>
        /// На що діє пасивка: виробіток, атака, захист, швидкість маршів чи вантажопідйомність.
        /// Пасивки складаються з бустами крамниці додаванням (GDD §5.10).
        /// </summary>
        public EffectTarget Effect { get; set; }

        /// <summary>Надбавка на 1 рівні звіра: 0.15 — +15%.</summary>
        public double BaseBonus { get; set; }

        /// <summary>Скільки додає кожен наступний рівень звіра.</summary>
        public double BonusPerLevel { get; set; }

        /// <summary>Скільки діє пасивка після активації.</summary>
        public int DurationMinutes { get; set; }

        /// <summary>Перезарядка від моменту активації; не коротша за дію.</summary>
        public int CooldownMinutes { get; set; }

        /// <summary>Скільки їжі зі складу коштує активація — стік для запасів без стелі.</summary>
        public int ActivationFood { get; set; }
    }
}
