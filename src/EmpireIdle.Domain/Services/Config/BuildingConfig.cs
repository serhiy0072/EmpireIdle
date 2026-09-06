namespace EmpireIdle.Domain.Services.Config
{
    /// <summary>
    /// Конфігурація одного типу будівлі.
    ///
    /// Поля розділені на три групи: спільні для всіх будівель, криві прогресії
    /// та спеціальні. Спеціальне поле читає лише та система, якій воно належить —
    /// решта будівель лишає його нулем. Це дешевше за ієрархію конфігів і
    /// не заважає reskin: нова механіка додає поле, а не тип.
    /// </summary>
    public class BuildingConfig
    {
        // ---------- Ідентичність ----------

        /// <summary>Унікальний ключ будівлі (наприклад "farm", "goldmine").</summary>
        public string Key { get; set; } = null!;

        /// <summary>Відображувана назва.</summary>
        public string DisplayName { get; set; } = null!;

        /// <summary>
        /// Головна будівля поселення — її рівень гейтить розблокування решти.
        /// Рівно одна на конфіг (townhall / command_center / bunker).
        /// </summary>
        public bool IsMainBuilding { get; set; }

        /// <summary>
        /// Мінімальний рівень головної будівлі для розблокування.
        /// Нижче цього порогу будівля існує в селищі, але схована туманом.
        /// </summary>
        public int RequiresMainBuildingLevel { get; set; }

        /// <summary>
        /// Позиція будівлі на карті селища. Домен її не використовує —
        /// це презентація, але тримається тут, щоб при reskin не синхронізувати два джерела.
        /// </summary>
        public BuildingPosition? Position { get; set; }

        // ---------- Виробництво і зберігання ----------

        /// <summary>Який ресурс виробляє ця будівля (null — не виробнича).</summary>
        public string? ProducesResource { get; set; }

        /// <summary>Базова кількість ресурсу за хвилину на 1 рівні. Росте лінійно з рівнем.</summary>
        public int BaseProductionPerMinute { get; set; }

        /// <summary>
        /// Базова місткість на 1 рівні. Для виробничої будівлі це буфер,
        /// для сховища — ліміт ресурсів у селищі. Росте лінійно з рівнем.
        /// </summary>
        public int BaseStorage { get; set; }

        /// <summary>
        /// Які ресурси зберігає ця будівля (null — не сховище).
        /// Склад тримає базові ресурси, банк — золото.
        /// </summary>
        public List<string>? StoresResources { get; set; }

        // ---------- Криві прогресії ----------

        /// <summary>Вартість апгрейду на 1 рівні — список пар «ресурс → кількість».</summary>
        public List<ResourceCost> Cost { get; set; } = new();

        /// <summary>
        /// Коефіцієнт росту вартості апгрейду: вартість = Cost × UpgradeCostGrowth^(рівень−1).
        /// Тримати нижчим за BuildTimeGrowth — тоді вузьке горло плавно
        /// мігрує з ресурсів на час, і прискорення продаються природно.
        /// </summary>
        public double UpgradeCostGrowth { get; set; }

        /// <summary>Базовий час апгрейду на 1 рівні, хвилин.</summary>
        public int BaseBuildMinutes { get; set; }

        /// <summary>Коефіцієнт росту часу апгрейду з рівнем: час = BaseBuildMinutes × BuildTimeGrowth^(рівень−1).</summary>
        public double BuildTimeGrowth { get; set; }

        // ---------- Спеціальні ----------

        /// <summary>Місткість поранених на рівень (0 — не госпіталь).</summary>
        public int WoundedCapacityPerLevel { get; set; }

        /// <summary>
        /// Частка бонусу до оборони за рівень стін, наприклад 0.03 = +3%.
        /// Множиться на рівень і додається захиснику в бойовій формулі.
        /// </summary>
        public double DefenceBonusPerLevel { get; set; }

        /// <summary>Радіус розвідки за рівень (0 — не вежа розвідників).</summary>
        public int ScoutRangePerLevel { get; set; }

        /// <summary>Скільки одночасних лотів на ринку відкриває рівень (0 — не ринок).</summary>
        public int MarketSlotsPerLevel { get; set; }

        /// <summary>
        /// Скільки чужих юнітів можна прийняти підкріпленням на рівень (0 — не посольство).
        /// Ліміт у господаря, не в того, хто надсилає.
        /// </summary>
        public int ReinforcementSlotsPerLevel { get; set; }

        /// <summary>Скільки приручених звірів вміщає рівень (0 — не звіринець).</summary>
        public int BeastCapacityPerLevel { get; set; }

        /// <summary>Скільки героїв можна тримати активними на рівень (0 — не зал героїв).</summary>
        public int HeroSlotsPerLevel { get; set; }

        /// <summary>Скільки одночасних прокачок зброї відкриває рівень (0 — не кузня).</summary>
        public int WeaponUpgradeSlotsPerLevel { get; set; }
    }
}
