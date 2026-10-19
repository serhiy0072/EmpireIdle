namespace EmpireIdle.Domain.Battles
{
    /// <summary>Що сталося в тику бою.</summary>
    public enum BattleEventKind
    {
        Hit,
        Death,

        /// <summary>Вампіризм чи вміння повернули здоров'я; Amount — скільки саме.</summary>
        Heal,

        /// <summary>Герой застосував уміння; Detail — його номер у FighterInfo.Skills.</summary>
        Cast,

        /// <summary>Вміння дало щит; Amount — його розмір.</summary>
        Shield,

        /// <summary>На бійця ліг стан; Detail — BattleStatusKind, Amount — скільки секунд.</summary>
        Status,

        /// <summary>
        /// Ворожу лінію прорвано — ближній боєць іде до наступної; Detail — номер лінії, на яку йде,
        /// Amount — скільки секунд іде.
        /// </summary>
        Advance
    }

    /// <summary>Подробиці удару для відтворення.</summary>
    [Flags]
    public enum HitFlags
    {
        None = 0,

        /// <summary>Тип нападника сильніший за тип цілі.</summary>
        Advantage = 1,

        /// <summary>Тип цілі сильніший за тип нападника.</summary>
        Disadvantage = 2,

        Crit = 4,

        /// <summary>Ціль заблокувала частину удару.</summary>
        Blocked = 8,

        /// <summary>Удар уміння, а не звичайна атака.</summary>
        Skill = 16,

        /// <summary>Отрута: Source і Target — сам отруєний.</summary>
        Poison = 32
    }

    /// <param name="Tick">Номер тику, з першого.</param>
    /// <param name="Source">Індекс бійця, що вдарив; для смерті — той самий, що й Target.</param>
    /// <param name="Amount">Шкода удару; для смерті — 0.</param>
    /// <param name="Detail">Номер уміння для Cast, вид стану для Status; інакше 0.</param>
    /// <param name="Health">
    /// Здоров'я цілі одразу після удару чи лікування — щит поглинає частину удару, і клієнт без цього
    /// не відтворив би смужку здоров'я; для решти подій — 0.
    /// </param>
    public readonly record struct BattleEvent(int Tick, BattleEventKind Kind, int Source, int Target, double Amount,
        HitFlags Flags = HitFlags.None, int Detail = 0, double Health = 0);

    /// <summary>Боєць на полі — те, що треба клієнту, щоб розставити фігурки.</summary>
    /// <param name="Leader">Індекс героя для конвою; null — це сам герой.</param>
    /// <param name="Ward">Щит оберегу на старті бою; живе RealtimeBattle.WardSeconds.</param>
    /// <param name="Skills">Ключі вмінь героя за номером, на який посилається Cast; у конвою — порожньо.</param>
    /// <param name="HeroKey">Ключ героя; у конвою — ключ його героя.</param>
    /// <param name="Line">Лінія вглиб поля, 0 — фронт (RealtimeBattle.HeroLines); конвой стоїть у лінії свого героя.</param>
    public sealed record FighterInfo(int Index, BattleSide Side, int Lane, int? Leader, string? UnitType, double MaxHealth, double Ward,
        IReadOnlyList<string> Skills, string? HeroKey, int Line);

    /// <summary>Підсумок бою: переможець, тривалість і повний лог для відтворення.</summary>
    /// <param name="Ticks">Скільки тиків тривав бій.</param>
    /// <param name="Health">Здоров'я кожного бійця наприкінці, за індексом.</param>
    public sealed record BattleResult(BattleSide Winner, int Ticks, IReadOnlyList<FighterInfo> Fighters,
        IReadOnlyList<double> Health, IReadOnlyList<BattleEvent> Events);
}
