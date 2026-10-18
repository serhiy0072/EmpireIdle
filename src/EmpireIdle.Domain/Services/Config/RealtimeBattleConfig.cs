namespace EmpireIdle.Domain.Services.Config
{
    /// <summary>
    /// Правила одномоментного бою арени й «Тисячної вежі» (рішення 16–18.10.2026): усі б'ються
    /// разом, удари за таймером атаки, вміння за перезарядкою. Данжі лишаються покроковими
    /// й цих чисел не читають. Шанс і сила криту, блок, вампіризм, зниження шкоди й оберіг
    /// мають ті самі стелі, що й у данжі (DungeonsConfig): це стати героя, а не режиму.
    /// </summary>
    public class RealtimeBattleConfig
    {
        /// <summary>Крок симуляції, мс: 100 — десять оновлень на секунду бою.</summary>
        public int TickMilliseconds { get; set; } = 100;

        /// <summary>Стеля тривалості бою, секунд: хто не переміг до неї — програв нападник.</summary>
        public int MaxBattleSeconds { get; set; } = 90;

        /// <summary>
        /// Класи героїв, що стоять у фронті й б'ють зблизька; решта — тил і стріляють. Конвої стоять
        /// і б'ють так само, як їхній герой.
        /// </summary>
        public List<string> FrontLineClasses { get; set; } = new();

        /// <summary>Скільки секунд ближній боєць іде до ворога, перш ніж бити.</summary>
        public double MeleeApproachSeconds { get; set; } = 1.5;

        /// <summary>Скільки секунд дальній боєць цілиться перед першим пострілом.</summary>
        public double RangedApproachSeconds { get; set; } = 0.5;

        /// <summary>
        /// Скільки очок захисту на очко атаки вдвічі гасять удар (рішення 18.10.2026: 2):
        /// частка = A / (A + D / DefensePerHalfDamage). Жодних рівнів чи сталих поза цим правилом.
        /// </summary>
        public double DefensePerHalfDamage { get; set; } = 2;

        /// <summary>Множник здоров'я героя з його статів: без нього перший герой гине за 2–4 секунди.</summary>
        public double HeroHealthScale { get; set; } = 4;

        /// <summary>Множник атаки конвою з атаки його юнітів.</summary>
        public double ConvoyAttackScale { get; set; } = 0.15;

        /// <summary>Множник здоров'я конвою з здоров'я його юнітів.</summary>
        public double ConvoyHealthScale { get; set; } = 1.0;

        /// <summary>Множник захисту конвою із захисту одного юніта: конвой — стрій, а не сума щитів.</summary>
        public double ConvoyDefenseScale { get; set; } = 12;

        /// <summary>Секунд між ударами героя без швидкості атаки — за класом героя.</summary>
        public Dictionary<string, double> HeroAttackIntervals { get; set; } = new();

        /// <summary>Секунд між ударами конвою — за типом юнітів; швидкість атаки героя на них не діє.</summary>
        public Dictionary<string, double> UnitAttackIntervals { get; set; } = new();

        /// <summary>
        /// Перевага типів (рішення 18.10.2026): тип юнітів → тип, який він б'є сильніше.
        /// Герой має тип своїх конвоїв (HeroSettings.RoleUnits).
        /// </summary>
        public Dictionary<string, string> TypeAdvantages { get; set; } = new();

        /// <summary>Множник шкоди по слабшому типу; по сильнішому — обернений.</summary>
        public double TypeAdvantageMultiplier { get; set; } = 1.35;

        /// <summary>Скільки секунд бою дорівнює одному ходу данжу: вміння в heroes.json записані в ходах.</summary>
        public double SecondsPerTurn { get; set; } = 2.5;

        /// <summary>Частка перезарядки, після якої вміння спрацьовує вперше.</summary>
        public double FirstCastShare { get; set; } = 0.5;

        /// <summary>Частка шкоди масового вміння по кожному бійцю загону цілі.</summary>
        public double AoeShare { get; set; } = 0.4;

        /// <summary>Скільки секунд живе щит оберегу від старту бою.</summary>
        public double WardSeconds { get; set; } = 15;

        /// <summary>
        /// Скорочення перезарядки за кожен хід CooldownTurns набору (tide, hollow): данж читає ходи,
        /// а цей бій — частку, тож набір працює в обох режимах без зміни items.json.
        /// </summary>
        public double CooldownReductionPerTurn { get; set; } = 0.12;

        /// <summary>Стеля швидкості атаки: +50% ударів.</summary>
        public double MaxAttackSpeed { get; set; } = 0.5;

        /// <summary>Стеля скорочення перезарядки разом із наборами.</summary>
        public double MaxCooldownReduction { get; set; } = 0.35;

        /// <summary>Скільки героїв у загоні.</summary>
        public int SquadSize { get; set; } = 5;
    }
}
