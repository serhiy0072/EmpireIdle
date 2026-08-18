namespace EmpireIdle.Domain.Enums
{
    /// <summary>Як рахується ціль.</summary>
    public enum ObjectiveMode
    {
        /// <summary>Лічильник росте від подій, назад не йде.</summary>
        Accumulate = 1,

        /// <summary>Перевіряється проти поточного стану — зараховується заднім числом.</summary>
        Threshold = 2
    }
}
