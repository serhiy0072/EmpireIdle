namespace EmpireIdle.Domain.Enums
{
    /// <summary>Куди телепорт може перенести поселення (GDD §8.9).</summary>
    public enum TeleportScope
    {
        /// <summary>Будь-яка вільна придатна клітина відкритої зони — обирає гравець.</summary>
        Exact = 0,

        /// <summary>Клітина, яку обирає гравець, не далі TeleportRange від нинішнього села.</summary>
        Nearby = 1,

        /// <summary>Клітина, яку обирає гравець, на території свого клану (у радіусі діючої споруди, §7.2).</summary>
        ClanTerritory = 2,

        /// <summary>Випадкова вільна придатна клітина відкритої зони; гравець нічого не обирає.</summary>
        Random = 3
    }
}
