namespace EmpireIdle.Domain.Services.Config
{
    /// <summary>
    /// Розвідка (GDD §5): марш розвідників із вежі розвідки. Без героя й юнітів,
    /// без обмежень на кількість і дальність. Числа — Режисера.
    /// </summary>
    public class ScoutingConfig
    {
        /// <summary>Будівля, яка відправляє розвідників; досить, щоб вона стояла. null — розвідки у світі немає.</summary>
        public string? RequiredBuilding { get; set; }

        /// <summary>
        /// Швидкість розвідників у клітинах за хвилину — як Speed юніта. Мусить бути вищою
        /// за найшвидший юніт: розвідка в рази швидша за військо.
        /// </summary>
        public double Speed { get; set; } = 24.0;
    }
}
