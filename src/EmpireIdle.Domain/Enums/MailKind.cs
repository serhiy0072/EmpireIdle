namespace EmpireIdle.Domain.Enums
{
    /// <summary>
    /// Тип особистого листа (GDD §7.4). Лист-посилання тримає лише тип і
    /// ReferenceId — актуальний стан читається при відкритті. Лист із
    /// нагородою несе вкладення сам і посилання не має.
    /// </summary>
    public enum MailKind
    {
        /// <summary>Запрошення в клан; ReferenceId — ClanRequest.</summary>
        ClanInvite = 1,

        /// <summary>Падіння міста; ReferenceId — VillageFall.</summary>
        CityFall = 2,

        /// <summary>Щоденна нагорода за вхід; Sequence — день серії.</summary>
        DailyReward = 3,

        /// <summary>Щотижнева нагорода за вхід.</summary>
        WeeklyReward = 4,

        /// <summary>Щомісячна нагорода за вхід.</summary>
        MonthlyReward = 5,

        /// <summary>Зруйновано споруду клану; ReferenceId — StructureFall.</summary>
        StructureFall = 6
    }
}
