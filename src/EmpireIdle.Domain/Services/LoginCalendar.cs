namespace EmpireIdle.Domain.Services
{
    /// <summary>
    /// Періоди нагород за вхід (GDD §8.12): доба, тиждень із понеділка й
    /// місяць — усе за UTC, однаково для всіх гравців світу.
    /// </summary>
    public static class LoginCalendar
    {
        public static DateOnly Day(DateTime utcNow) => DateOnly.FromDateTime(utcNow);

        /// <summary>Понеділок тижня, до якого належить день.</summary>
        public static DateOnly WeekStart(DateOnly day) => day.AddDays(-(((int)day.DayOfWeek + 6) % 7));

        public static DateOnly MonthStart(DateOnly day) => new(day.Year, day.Month, 1);

        /// <summary>Початок наступної доби — тоді згорає щоденна нагорода.</summary>
        public static DateTime DayEnds(DateOnly day) => Midnight(day.AddDays(1));

        public static DateTime WeekEnds(DateOnly day) => Midnight(WeekStart(day).AddDays(7));

        public static DateTime MonthEnds(DateOnly day) => Midnight(MonthStart(day).AddMonths(1));

        private static DateTime Midnight(DateOnly day) => day.ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc);
    }
}
