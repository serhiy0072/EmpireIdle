namespace EmpireIdle.Domain.Enums
{
    /// <summary>Коли квест доступний.</summary>
    public enum QuestWindow
    {
        /// <summary>Назавжди: стартова історія, віхи.</summary>
        Chain = 1,

        /// <summary>Щоденний, обнуляється о 00:00 UTC.</summary>
        Daily = 2,

        /// <summary>Обмежений діапазоном дат.</summary>
        Event = 3
    }
}
