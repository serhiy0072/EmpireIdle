namespace EmpireIdle.Domain.Enums
{
    /// <summary>
    /// Кого вміння по ворогу бере першим в одномоментному бою (рішення 19.10.2026: ціль — для кожного
    /// вміння своя). Обраної лінії вже немає — вміння б'є найближчого, як звичайний удар.
    /// </summary>
    public enum SkillFocus
    {
        /// <summary>Як звичайний удар: найближча доріжка, ближній — лише найпередня жива лінія.</summary>
        Nearest = 0,

        /// <summary>Перша лінія ворога — воїни.</summary>
        FrontLine = 1,

        /// <summary>Друга лінія — лицарі.</summary>
        MiddleLine = 2,

        /// <summary>Найдальша лінія — стрільці.</summary>
        BackLine = 3,

        /// <summary>Ворожий герой із найбільшою атакою, хоч би де стояв.</summary>
        Strongest = 4
    }
}
