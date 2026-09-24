using EmpireIdle.Domain.Entities;
using EmpireIdle.Domain.Enums;
using EmpireIdle.Domain.Exceptions;
using EmpireIdle.Domain.Services;
using EmpireIdle.Domain.ValueObjects;

namespace EmpireIdle.Domain.Tests.Entities
{
    /// <summary>
    /// Нагороди за вхід (GDD §8.12): серія днів поспіль, тиждень із понеділка
    /// й місяць за UTC, і лист із вкладенням, яке забирають один раз.
    /// </summary>
    public class LoginRewardProgressTests
    {
        private const int Cycle = 7;

        // 2026-09-21 — понеділок
        private static readonly DateOnly Monday = new(2026, 9, 21);

        private static LoginRewardProgress NewProgress() => new(Guid.NewGuid(), Guid.NewGuid(), 1);

        // ---------- Серія ----------

        [Fact]
        public void CheckIn_ShouldGiveEverything_OnTheFirstVisit()
        {
            var due = NewProgress().CheckIn(Monday, Cycle);

            Assert.Equal(new LoginRewardsDue(1, true, true), due);
        }

        [Fact]
        public void CheckIn_ShouldGiveNothing_OnASecondVisitTheSameDay()
        {
            var progress = NewProgress();
            progress.CheckIn(Monday, Cycle);

            Assert.False(progress.CheckIn(Monday, Cycle).Any);
        }

        [Fact]
        public void CheckIn_ShouldExtendTheStreak_OnConsecutiveDays()
        {
            var progress = NewProgress();
            progress.CheckIn(Monday, Cycle);

            Assert.Equal(2, progress.CheckIn(Monday.AddDays(1), Cycle).DailyDay);
            Assert.Equal(3, progress.CheckIn(Monday.AddDays(2), Cycle).DailyDay);
        }

        [Fact]
        public void CheckIn_ShouldRestartTheStreak_AfterAMissedDay()
        {
            var progress = NewProgress();
            progress.CheckIn(Monday, Cycle);
            progress.CheckIn(Monday.AddDays(1), Cycle);

            Assert.Equal(1, progress.CheckIn(Monday.AddDays(3), Cycle).DailyDay);
        }

        /// <summary>Восьмий день поспіль починає нову серію з першого дня.</summary>
        [Fact]
        public void CheckIn_ShouldStartANewSeries_AfterTheLastDay()
        {
            var progress = NewProgress();

            for (var i = 0; i < Cycle; i++)
                progress.CheckIn(Monday.AddDays(i), Cycle);

            Assert.Equal(1, progress.CheckIn(Monday.AddDays(Cycle), Cycle).DailyDay);
        }

        [Fact]
        public void CheckIn_ShouldGiveNoDailyReward_WhenNoneIsConfigured()
        {
            Assert.Null(NewProgress().CheckIn(Monday, cycleLength: 0).DailyDay);
        }

        // ---------- Тиждень і місяць ----------

        [Fact]
        public void CheckIn_ShouldGiveTheWeeklyReward_OncePerWeek()
        {
            var progress = NewProgress();
            progress.CheckIn(Monday, Cycle);

            Assert.False(progress.CheckIn(Monday.AddDays(6), Cycle).Weekly);
            Assert.True(progress.CheckIn(Monday.AddDays(7), Cycle).Weekly);
        }

        [Fact]
        public void CheckIn_ShouldGiveTheMonthlyReward_OncePerMonth()
        {
            var progress = NewProgress();
            progress.CheckIn(new DateOnly(2026, 9, 1), Cycle);

            Assert.False(progress.CheckIn(new DateOnly(2026, 9, 30), Cycle).Monthly);
            Assert.True(progress.CheckIn(new DateOnly(2026, 10, 1), Cycle).Monthly);
        }

        // ---------- Календар ----------

        [Fact]
        public void Calendar_ShouldStartTheWeekOnMonday()
        {
            Assert.Equal(Monday, LoginCalendar.WeekStart(Monday.AddDays(6)));
            Assert.Equal(Monday, LoginCalendar.WeekStart(Monday));
        }

        [Fact]
        public void Calendar_ShouldEndPeriodsAtUtcMidnight()
        {
            var wednesday = Monday.AddDays(2);

            Assert.Equal(new DateTime(2026, 9, 24, 0, 0, 0, DateTimeKind.Utc), LoginCalendar.DayEnds(wednesday));
            Assert.Equal(new DateTime(2026, 9, 28, 0, 0, 0, DateTimeKind.Utc), LoginCalendar.WeekEnds(wednesday));
            Assert.Equal(new DateTime(2026, 10, 1, 0, 0, 0, DateTimeKind.Utc), LoginCalendar.MonthEnds(wednesday));
        }

        // ---------- Вкладення листа ----------

        private static readonly DateTime Now = new(2026, 9, 21, 10, 0, 0, DateTimeKind.Utc);

        private static MailLetter RewardLetter()
            => MailLetter.WithRewards(Guid.NewGuid(), 1, Guid.NewGuid(), MailKind.DailyReward,
                [new MailReward("Gems", null, 10)], 1, Now, Now.AddHours(14));

        [Fact]
        public void Claim_ShouldHandOverTheRewardsOnce()
        {
            var letter = RewardLetter();

            Assert.Single(letter.Claim(Now.AddHours(1)));
            Assert.True(letter.IsRead);

            var again = Assert.Throws<RequirementNotMetException>(() => letter.Claim(Now.AddHours(2)));
            Assert.Equal(RefusalReasons.MailNothingToClaim.Key, again.Reason);
        }

        /// <summary>Незабране вкладення згорає разом зі строком листа.</summary>
        [Fact]
        public void Claim_ShouldRefuse_AfterTheLetterExpired()
        {
            var letter = RewardLetter();

            var refusal = Assert.Throws<RequirementNotMetException>(() => letter.Claim(Now.AddHours(14)));

            Assert.Equal(RefusalReasons.MailLetterExpired.Key, refusal.Reason);
            Assert.False(letter.CanClaimAt(Now.AddHours(14)));
        }

        [Fact]
        public void Claim_ShouldRefuse_ALetterWithoutRewards()
        {
            var letter = new MailLetter(Guid.NewGuid(), 1, Guid.NewGuid(), MailKind.ClanInvite, Guid.NewGuid(), Now,
                TimeSpan.FromDays(14));

            Assert.Throws<RequirementNotMetException>(() => letter.Claim(Now));
        }
    }
}
