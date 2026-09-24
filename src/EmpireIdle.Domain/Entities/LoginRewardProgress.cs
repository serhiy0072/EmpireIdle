using EmpireIdle.Domain.Services;
using EmpireIdle.Domain.ValueObjects;

namespace EmpireIdle.Domain.Entities
{
    /// <summary>
    /// Прогрес нагород за вхід гравця (GDD §8.12): серія днів і відмітки,
    /// за який тиждень і місяць нагороду вже видано. Рядок на гравця.
    /// </summary>
    public class LoginRewardProgress : Entity
    {
        public Guid PlayerId { get; private set; }

        public int ServerId { get; private set; }

        /// <summary>Останній день, коли гравець заходив.</summary>
        public DateOnly? LastCheckIn { get; private set; }

        /// <summary>День поточної серії, 1…довжина циклу.</summary>
        public int Streak { get; private set; }

        /// <summary>Понеділок тижня, за який уже видано щотижневу нагороду.</summary>
        public DateOnly? RewardedWeek { get; private set; }

        /// <summary>Перше число місяця, за який уже видано щомісячну нагороду.</summary>
        public DateOnly? RewardedMonth { get; private set; }

        /// <summary>Concurrency token (PostgreSQL xmin): дві вкладки не видадуть день двічі.</summary>
        public uint Version { get; private set; }

        public LoginRewardProgress(Guid id, Guid playerId, int serverId) : base(id)
        {
            PlayerId = playerId;
            ServerId = serverId;
        }

        protected LoginRewardProgress() { } // Для EF Core

        /// <summary>
        /// Відмічає вхід. Повторний того ж дня нічого не дає. Вчорашній вхід
        /// продовжує серію, пропуск починає її заново; після останнього дня
        /// циклу серія теж починається з першого (GDD §8.12).
        /// </summary>
        /// <param name="cycleLength">Скільки днів у серії; 0 — щоденних нагород немає.</param>
        public LoginRewardsDue CheckIn(DateOnly today, int cycleLength)
        {
            if (LastCheckIn == today)
                return LoginRewardsDue.Nothing;

            Streak = LastCheckIn == today.AddDays(-1) && cycleLength > 0
                ? Streak % cycleLength + 1
                : 1;

            LastCheckIn = today;

            var week = LoginCalendar.WeekStart(today);
            var weekly = RewardedWeek != week;
            RewardedWeek = week;

            var month = LoginCalendar.MonthStart(today);
            var monthly = RewardedMonth != month;
            RewardedMonth = month;

            return new LoginRewardsDue(cycleLength > 0 ? Streak : null, weekly, monthly);
        }
    }
}
