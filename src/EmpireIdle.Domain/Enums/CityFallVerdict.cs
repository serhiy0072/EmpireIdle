namespace EmpireIdle.Domain.Enums
{
    /// <summary>Чим закінчилась перевірка запобіжників падіння міста (GDD §2.6).</summary>
    public enum CityFallVerdict
    {
        /// <summary>Запобіжники пройдено — село виселяється.</summary>
        Evict = 1,

        /// <summary>Механіку вимкнено конфігом світу.</summary>
        Disabled = 2,

        /// <summary>Нападник сильніший за поріг — перемога без виселення.</summary>
        AttackerTooStrong = 3,

        /// <summary>Нападник вичерпав ліміт виселень за вікно.</summary>
        LimitReached = 4
    }
}
