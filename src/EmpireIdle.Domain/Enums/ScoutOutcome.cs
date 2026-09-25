namespace EmpireIdle.Domain.Enums
{
    /// <summary>Чим скінчилась розвідка.</summary>
    public enum ScoutOutcome
    {
        /// <summary>Дані зібрано: що можна винести й сила оборони з усіма бонусами.</summary>
        Success = 1,

        /// <summary>У цілі діє блокувальник розвідки — даних немає, строк не розкривається.</summary>
        Blocked = 2,

        /// <summary>Ціль переселилась, поки йшли розвідники, — на місці порожньо.</summary>
        TargetMoved = 3,

        /// <summary>Ціль зникла: село покинуто чи споруду зруйновано.</summary>
        TargetGone = 4
    }
}
