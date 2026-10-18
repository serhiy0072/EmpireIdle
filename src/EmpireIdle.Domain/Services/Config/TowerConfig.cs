using EmpireIdle.Domain.Enums;

namespace EmpireIdle.Domain.Services.Config
{
    /// <summary>
    /// «Тисячна вежа» (рішення 18.10.2026): поверхи по черзі, кожен проходиться раз, нагорода одноразова,
    /// спроби безлімітні й безплатні. У конфігу немає тисячі записів — поверх будується з номера:
    /// опорні точки кривої, ступені набору на ворогах, ротація складів і нагорода за віхами.
    /// </summary>
    public class TowerConfig
    {
        /// <summary>Рівень ратуші, з якого відкривається вежа.</summary>
        public int RequiresMainBuildingLevel { get; set; } = 3;

        /// <summary>Найвищий поверх; пройдений він — вежу підкорено.</summary>
        public int MaxFloor { get; set; } = 1000;

        /// <summary>
        /// Опорні точки кривої ворога від першого поверху до MaxFloor: між ними рівні інтерполюються
        /// лінійно, множник статів — геометрично.
        /// </summary>
        public List<TowerAnchorConfig> Anchors { get; set; } = new();

        /// <summary>Набір на ворогах — множником статів, що росте ступенями з поверхом.</summary>
        public List<TowerSetStepConfig> SetSteps { get; set; } = new();

        /// <summary>Склади ворога за доріжками; звичайні поверхи чергують їх, щоб гравець перебудовував загін.</summary>
        public List<TowerArchetypeConfig> Archetypes { get; set; } = new();

        /// <summary>Кожен який поверх — бос.</summary>
        public int BossEvery { get; set; } = 10;

        /// <summary>Склад загону боса — ключ з Archetypes.</summary>
        public string BossArchetype { get; set; } = null!;

        /// <summary>Доріжка, на якій стоїть сам бос.</summary>
        public int BossLane { get; set; } = 2;

        /// <summary>Множник здоров'я героя-боса.</summary>
        public double BossHealthScale { get; set; } = 2.5;

        /// <summary>Рідкості героїв-ворогів ступенями з поверхом: вище — рідкісніші шаблони.</summary>
        public List<TowerRarityStepConfig> HeroRarities { get; set; } = new();

        /// <summary>Ресурс, яким платить кожен поверх.</summary>
        public string GoldResource { get; set; } = "gold";

        /// <summary>Золото поверху: GoldBase + GoldPerFloor × номер.</summary>
        public int GoldBase { get; set; } = 200;

        public int GoldPerFloor { get; set; } = 10;

        /// <summary>
        /// Нагорода понад золото: з віх, що ділять номер поверху, діє та, у якої найбільший Every —
        /// вища віха замінює нижчу. Віха з Every 1 — нагорода звичайного поверху.
        /// </summary>
        public List<TowerMilestoneConfig> Milestones { get; set; } = new();
    }

    /// <summary>Опорна точка кривої ворога.</summary>
    public class TowerAnchorConfig
    {
        public int Floor { get; set; }

        public int HeroLevel { get; set; }

        public int UnitLevel { get; set; }

        /// <summary>Рівень умінь героїв-ворогів.</summary>
        public int SkillLevel { get; set; } = 1;

        /// <summary>Множник атаки, захисту й здоров'я героїв і конвоїв ворога.</summary>
        public double StatMultiplier { get; set; } = 1;
    }

    /// <summary>Ступінь набору на ворогах: з цього поверху стати множаться на Multiplier.</summary>
    public class TowerSetStepConfig
    {
        public int FromFloor { get; set; }

        public double Multiplier { get; set; } = 1;

        /// <summary>Що носять вороги — для екрана підготовки («4 рідкісні»).</summary>
        public string DisplayName { get; set; } = string.Empty;
    }

    /// <summary>Склад ворога: клас героя на кожній доріжці.</summary>
    public class TowerArchetypeConfig
    {
        public string Key { get; set; } = null!;

        public string DisplayName { get; set; } = null!;

        /// <summary>Класи за доріжками, з нульової.</summary>
        public List<string> Classes { get; set; } = new();
    }

    /// <summary>З цього поверху вороги — герої лише цих рідкостей.</summary>
    public class TowerRarityStepConfig
    {
        public int FromFloor { get; set; }

        public List<Rarity> Rarities { get; set; } = new();
    }

    /// <summary>Віха нагороди: кожен Every-й поверх.</summary>
    public class TowerMilestoneConfig
    {
        public int Every { get; set; }

        /// <summary>Нагорода ступенями з поверхом: діє останній ступінь, чий FromFloor не вищий за поверх.</summary>
        public List<TowerRewardStageConfig> Stages { get; set; } = new();
    }

    /// <summary>
    /// Ступінь нагороди віхи. Кілька варіантів чергуються від віхи до віхи: уламок, скриня, уламок…
    /// </summary>
    public class TowerRewardStageConfig
    {
        public int FromFloor { get; set; } = 1;

        public List<List<RewardConfig>> Options { get; set; } = new();
    }
}
