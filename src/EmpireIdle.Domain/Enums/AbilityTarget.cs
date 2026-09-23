namespace EmpireIdle.Domain.Enums
{
    /// <summary>Кого зачіпає вміння. Вибір цілі гравцем має сенс лише для одноцільових.</summary>
    public enum AbilityTarget
    {
        /// <summary>Один ворог. Підлягає правилу ліній, якщо вміння не ігнорує його.</summary>
        SingleEnemy = 0,

        /// <summary>Усі живі вороги — правило ліній не діє.</summary>
        AllEnemies = 1,

        /// <summary>Сам виконавець.</summary>
        Self = 2,

        /// <summary>Один союзник; авто-політика бере найпораненішого.</summary>
        SingleAlly = 3,

        /// <summary>Уся жива команда.</summary>
        AllAllies = 4
    }
}
