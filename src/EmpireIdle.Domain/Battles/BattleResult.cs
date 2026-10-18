namespace EmpireIdle.Domain.Battles
{
    /// <summary>Що сталося в тику бою.</summary>
    public enum BattleEventKind
    {
        Hit,
        Death
    }

    /// <summary>Подробиці удару для відтворення.</summary>
    [Flags]
    public enum HitFlags
    {
        None = 0,

        /// <summary>Тип нападника сильніший за тип цілі.</summary>
        Advantage = 1,

        /// <summary>Тип цілі сильніший за тип нападника.</summary>
        Disadvantage = 2
    }

    /// <param name="Tick">Номер тику, з першого.</param>
    /// <param name="Source">Індекс бійця, що вдарив; для смерті — той самий, що й Target.</param>
    /// <param name="Amount">Шкода удару; для смерті — 0.</param>
    public readonly record struct BattleEvent(int Tick, BattleEventKind Kind, int Source, int Target, double Amount, HitFlags Flags = HitFlags.None);

    /// <summary>Боєць на полі — те, що треба клієнту, щоб розставити фігурки.</summary>
    /// <param name="Leader">Індекс героя для конвою; null — це сам герой.</param>
    public sealed record FighterInfo(int Index, BattleSide Side, int Lane, int? Leader, string? UnitType, double MaxHealth);

    /// <summary>Підсумок бою: переможець, тривалість і повний лог для відтворення.</summary>
    /// <param name="Ticks">Скільки тиків тривав бій.</param>
    /// <param name="Health">Здоров'я кожного бійця наприкінці, за індексом.</param>
    public sealed record BattleResult(BattleSide Winner, int Ticks, IReadOnlyList<FighterInfo> Fighters,
        IReadOnlyList<double> Health, IReadOnlyList<BattleEvent> Events);
}
