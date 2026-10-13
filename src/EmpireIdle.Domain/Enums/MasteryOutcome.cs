namespace EmpireIdle.Domain.Enums
{
    /// <summary>Чим закінчилась спроба заточки (GDD §9.12).</summary>
    public enum MasteryOutcome
    {
        /// <summary>Заточка піднялась на ранг.</summary>
        Success = 1,

        /// <summary>Невдача: золото втрачено, заточка та сама.</summary>
        Failed = 2,

        /// <summary>Невдача з поломкою: предмет дає половину статів, доки його не відремонтують.</summary>
        Broken = 3
    }
}
