namespace EmpireIdle.Domain.Enums
{
    /// <summary>
    /// Стан на бійцеві. Тримається лічильником ходів і згасає сам;
    /// повторне накладання оновлює тривалість, а не складає ефекти —
    /// інакше два маги замикали б ворога в оглушенні назавжди.
    /// </summary>
    public enum BattleStatusKind
    {
        /// <summary>Шкода щоходу; magnitude — скільки саме.</summary>
        Poison = 0,

        /// <summary>Пропуск ходу.</summary>
        Stun = 1,

        /// <summary>Множник атаки: magnitude — частка (0.25 = +25%).</summary>
        AttackUp = 2,

        /// <summary>Множник захисту.</summary>
        DefenseUp = 3,

        /// <summary>Від'ємний множник захисту цілі.</summary>
        DefenseDown = 4,

        /// <summary>Поглинає шкоду, поки не витратиться; тривалість теж обмежена.</summary>
        Shield = 5,

        /// <summary>Тягне на себе одноцільові удари ворога, поки тримається.</summary>
        Taunt = 6
    }
}
