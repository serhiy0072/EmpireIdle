namespace EmpireIdle.Domain.Services.Config
{
    /// <summary>
    /// Кланова територія (GDD §7.2): споруди на карті дають сталий бонус усім
    /// військам клану, що б'ються в їхньому радіусі. Числа — заглушки до Режисера.
    /// </summary>
    public class ClanTerritoryConfig
    {
        /// <summary>
        /// Вимкнено за замовчуванням: механіка вмикається конфігом світу,
        /// і світи без неї (та тестові конфіги) споруд не мають.
        /// </summary>
        public bool Enabled { get; set; }

        /// <summary>Радіус покриття споруди в клітинах (квадрат, як кільця карти).</summary>
        public int Radius { get; set; } = 5;

        /// <summary>Стеля споруд на клан.</summary>
        public int MaxStructures { get; set; } = 10;

        /// <summary>Скільки слотів відкрито клану одразу.</summary>
        public int StartingSlots { get; set; } = 5;

        /// <summary>Кожен запис відкриває ще один слот: за квест клану або за кількість учасників.</summary>
        public List<ClanSlotUnlockConfig> SlotUnlocks { get; set; } = [];

        /// <summary>Скільки очок вкладу коштує закласти споруду.</summary>
        public long StructureCostPoints { get; set; } = 1000;

        /// <summary>Скільки хвилин будується споруда без допомоги маршів.</summary>
        public int BuildMinutes { get; set; } = 24 * 60;

        /// <summary>
        /// Яку частку повного будівництва зрізає одиниця сили маршу. Один сильний
        /// марш прискорює більше за кілька слабких — сила, а не кількість маршів.
        /// </summary>
        public double BuildSharePerPower { get; set; } = 0.0005;

        /// <summary>Найбільша частка будівництва, яку можуть зрізати марші: 1 — клан у повній явці будує миттєво.</summary>
        public double MaxBuildShare { get; set; } = 1.0;

        /// <summary>Скільки юнітів вміщає гарнізон споруди.</summary>
        public int GarrisonCapacity { get; set; } = 500;

        /// <summary>Множник атаки військ клану в радіусі споруди (0.1 = +10%).</summary>
        public double AttackBonus { get; set; } = 0.1;

        /// <summary>Множник оборони військ клану в радіусі споруди (0.1 = +10%).</summary>
        public double DefenceBonus { get; set; } = 0.1;

        /// <summary>Частка здобичі з перемоги над монстром, яку клан отримує очками вкладу. Гравцю нагорода не зменшується.</summary>
        public double MonsterLootCashbackShare { get; set; } = 0.05;
    }

    /// <summary>Умова відкриття одного слота: рівно одне з двох.</summary>
    public class ClanSlotUnlockConfig
    {
        /// <summary>Слот відкривається, щойно в клані стільки учасників.</summary>
        public int? MinMembers { get; set; }

        /// <summary>Слот відкривається виконанням кланового квесту з цим ключем.</summary>
        public string? QuestKey { get; set; }
    }
}
