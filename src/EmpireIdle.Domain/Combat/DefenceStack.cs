using EmpireIdle.Domain.ValueObjects;

namespace EmpireIdle.Domain.Combat
{
    /// <summary>
    /// Однорідна частина оборони: чиї юніти, якого типу й скільки.
    ///
    /// Власник потрібен, бо втрати адресні: свої юніти лягають у госпіталь
    /// господаря, чужі — власника підкріплення.
    /// </summary>
    /// <param name="OwnerPlayerId">
    /// null — юніти самого гарнізону. Гарнізон не знає id свого гравця
    /// (він прив'язаний до села), тож «свої» позначаються відсутністю власника.
    /// </param>
    public record DefenceStack(Guid? OwnerPlayerId, string UnitType, int Level, int Count);

    /// <summary>Оборона без власників: монстри, прев'ю, тести.</summary>
    public static class DefenceStacks
    {
        /// <summary>
        /// Армія одним власником — по стеку на тип+рівень. Там, де героїв бути
        /// не може, бій усе одно приймає стеки, тож перетворення тут.
        /// </summary>
        public static IReadOnlyList<DefenceStack> FromArmy(IReadOnlyDictionary<UnitStackKey, int> army)
            => army.Where(u => u.Value > 0)
            .Select(u => new DefenceStack(null, u.Key.UnitType, u.Key.Level, u.Value))
            .ToList();
    }
}
