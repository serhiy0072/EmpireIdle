namespace EmpireIdle.Domain.Services.Config
{
    /// <summary>
    /// Технології (рішення 28.10.2026): особисті досліджуються в Лабораторії за ресурси й час,
    /// кланові вивчаються підношеннями учасників. Дерево вкладки — блок вузлів, що повторюється тірами I, II, III…:
    /// той самий вузол на вищому тірі дорожчий і, для бойових ефектів, сильніший.
    /// </summary>
    public class TechnologiesConfig
    {
        public PersonalTechnologiesConfig Personal { get; set; } = new();

        public ClanTechnologiesConfig Clan { get; set; } = new();
    }

    /// <summary>
    /// Що змінює рівень технології. Відсотки швидкості скорочують тривалість як база / (1 + Σ),
    /// решта відсотків додається до свого пулу; ConvoySize і OfferingAttempts — цілі числа.
    /// </summary>
    public enum TechnologyStat
    {
        // ---------- Бій і марш ----------
        Attack,
        Defense,
        ConvoySize,
        MarchSpeed,
        Carry,

        // ---------- Економіка й розвиток ----------
        Production,
        MealUpkeep,
        ConstructionSpeed,
        TrainingSpeed,
        UnitLevelUpSpeed,
        ResearchSpeed,
        WoundedCapacity,
        UnitCapacity,
        HeroExperience,
        TameChance,
        ProtectedStorage,

        // ---------- Лише кланові ----------
        HelpShareCap,
        OfferingAttempts,
        CoinBonus,
        TerritoryAttack,
        TerritoryDefense,
        StructureBuildSpeed,
        StructureGarrison,
        StructureCost,
        LootCashback
    }

    /// <summary>Яка шкала тіру підсилює бонус вузла на вищих тірах; None — бонус однаковий на кожному тірі.</summary>
    public enum TechnologyScaling
    {
        None,
        Battle,
        Convoy
    }

    /// <summary>Один ефект вузла: стат, ціль (тип юніта чи ресурс; порожньо — усі) і бонус за рівень на першому тірі.</summary>
    public class TechnologyEffectConfig
    {
        public TechnologyStat Stat { get; set; }

        /// <summary>Тип юніта (Attack, Defense, ConvoySize) або ресурс (Production); null — для всіх.</summary>
        public string? Target { get; set; }

        public double PerLevel { get; set; }
    }

    /// <summary>
    /// Вузол дерева. Той самий вузол повторюється на кожному тірі; передумови — вузли тієї ж вкладки й тіру
    /// з меншим рядом. Перший ряд тіру відкривається, коли закрито останній ряд попереднього тіру.
    /// </summary>
    public class TechnologyNodeConfig
    {
        public string Key { get; set; } = null!;

        /// <summary>Вкладка зі списку Tabs свого розділу.</summary>
        public string Tab { get; set; } = null!;

        /// <summary>Ряд у блоці тіру, від 1: від нього росте ціна, і передумови лише з менших рядів.</summary>
        public int Row { get; set; }

        /// <summary>Вузли, з яких треба вивчити хоча б один рівень, щоб відкрити цей.</summary>
        public List<string> Requires { get; set; } = new();

        public int Levels { get; set; }

        public TechnologyScaling Scaling { get; set; }

        public List<TechnologyEffectConfig> Effects { get; set; } = new();

        /// <summary>Лише кланові: якими ресурсами можна підносити на цей вузол.</summary>
        public List<string> OfferResources { get; set; } = new();
    }

    /// <summary>Тір дерева: гейти відкриття, множники ціни й шкали ефектів.</summary>
    public class TechnologyTierConfig
    {
        public int Tier { get; set; }

        /// <summary>Рівень ратуші, з якого тір відкривається (лише особисті; 0 — без гейту).</summary>
        public int MainBuildingLevel { get; set; }

        /// <summary>Рівень світу, з якого тір відкривається.</summary>
        public int WorldLevel { get; set; } = 1;

        /// <summary>Особисті: множник часу дослідження тіру.</summary>
        public double TimeMultiplier { get; set; } = 1;

        /// <summary>Особисті: опорний рівень ратуші — від нього ціна в ресурсах за хвилину.</summary>
        public int ReferenceTownHall { get; set; } = 1;

        /// <summary>Кланові: множник очок підношень тіру.</summary>
        public double PointsMultiplier { get; set; } = 1;

        /// <summary>Множник бонусу вузлів за шкалою; шкали немає в словнику — множник 1.</summary>
        public Dictionary<TechnologyScaling, double> Scales { get; set; } = new();
    }

    /// <summary>
    /// Особисті технології. Час рівня: BaseMinutes × тір.TimeMultiplier × RowTimeGrowth^(ряд−1) × LevelTimeGrowth^(рівень−1).
    /// Ціна ресурсу: хвилини × CostPerMinutePerTownHall × тір.ReferenceTownHall × вага ресурсу вкладки, з округленням.
    /// </summary>
    public class PersonalTechnologiesConfig
    {
        /// <summary>Будівля без рівнів, де йдуть дослідження; null — особистих технологій немає.</summary>
        public string? BuildingKey { get; set; }

        public List<string> Tabs { get; set; } = new();

        public double BaseMinutes { get; set; } = 15;

        public double RowTimeGrowth { get; set; } = 1.3;

        public double LevelTimeGrowth { get; set; } = 1.25;

        public double CostPerMinutePerTownHall { get; set; } = 5;

        /// <summary>Ціна округлюється до кратного цього числа.</summary>
        public int CostRounding { get; set; } = 50;

        /// <summary>Вкладка → ресурс → вага ціни.</summary>
        public Dictionary<string, Dictionary<string, double>> TabResources { get; set; } = new();

        /// <summary>Скільки учасників клану можуть допомогти одному дослідженню (по одній допомозі).</summary>
        public int MaxHelps { get; set; } = 50;

        /// <summary>Частка повного часу, яку зрізає одна допомога.</summary>
        public double HelpShare { get; set; } = 0.005;

        public List<TechnologyTierConfig> Tiers { get; set; } = new();

        public List<TechnologyNodeConfig> Nodes { get; set; } = new();
    }

    /// <summary>
    /// Кланові технології. Очки рівня: BasePoints × тір.PointsMultiplier × RowGrowth^(ряд−1) × LevelGrowth^(рівень−1),
    /// з округленням до PointsRounding.
    /// </summary>
    public class ClanTechnologiesConfig
    {
        public List<string> Tabs { get; set; } = new();

        public double BasePoints { get; set; } = 1500;

        public double RowGrowth { get; set; } = 1.2;

        public double LevelGrowth { get; set; } = 1.3;

        public int PointsRounding { get; set; } = 100;

        public List<TechnologyTierConfig> Tiers { get; set; } = new();

        public List<TechnologyNodeConfig> Nodes { get; set; } = new();

        public ClanOfferingConfig Offering { get; set; } = new();

        public ClanCoinsConfig Coins { get; set; } = new();

        public ClanShopConfig Shop { get; set; } = new();
    }

    /// <summary>Підношення: спроби ресурсами з поступовим відновленням і окремі підношення за gems.</summary>
    public class ClanOfferingConfig
    {
        public int MaxAttempts { get; set; } = 25;

        /// <summary>За скільки секунд відновлюється одна спроба.</summary>
        public int AttemptRechargeSeconds { get; set; } = 576;

        public int PointsPerAttempt { get; set; } = 10;

        /// <summary>Ресурс → ціна однієї спроби за кожен рівень ратуші гравця.</summary>
        public Dictionary<string, int> AttemptCostPerTownHall { get; set; } = new();

        public int GemPoints { get; set; } = 50;

        /// <summary>Ціна n-го gems-підношення за добу UTC: min(GemPriceStep × n, GemPriceCap).</summary>
        public int GemPriceStep { get; set; } = 20;

        public int GemPriceCap { get; set; } = 400;

        public int GemDailyLimit { get; set; } = 50;
    }

    /// <summary>Кланові монети за підношення: база × множник, який кидає сервер.</summary>
    public class ClanCoinsConfig
    {
        public int BaseAmount { get; set; } = 100;

        public List<CoinMultiplierConfig> Multipliers { get; set; } = new();
    }

    public class CoinMultiplierConfig
    {
        public int Multiplier { get; set; }

        public double Weight { get; set; }
    }

    /// <summary>Клановий магазин: щоденна вітрина з пулу (однакова для всіх за датою) і фіксована тижнева.</summary>
    public class ClanShopConfig
    {
        public int DailySlots { get; set; } = 6;

        public List<ClanShopItemConfig> DailyPool { get; set; } = new();

        public List<ClanShopItemConfig> Weekly { get; set; } = new();
    }

    public class ClanShopItemConfig
    {
        public string ItemKey { get; set; } = null!;

        public int Price { get; set; }

        /// <summary>Скільки разів можна купити за період вітрини.</summary>
        public int Limit { get; set; }
    }
}
