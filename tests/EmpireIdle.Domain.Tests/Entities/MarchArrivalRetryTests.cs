using EmpireIdle.Domain.Entities;
using EmpireIdle.Domain.Enums;
using EmpireIdle.Domain.ValueObjects;

namespace EmpireIdle.Domain.Tests.Entities
{
    /// <summary>
    /// Прибуття, що падає стабільно, не повторюється кожні дві секунди: пауза росте
    /// разом із простроченням — від 10 секунд до години.
    /// </summary>
    public class MarchArrivalRetryTests
    {
        private static readonly DateTime ArrivesAt = new(2026, 10, 26, 12, 0, 0, DateTimeKind.Utc);

        private static March NewMarch()
            => new(Guid.NewGuid(), 1, Guid.NewGuid(), 1, 1, 5, 5, MarchTargetType.Monster, Guid.NewGuid(),
                new Dictionary<UnitStackKey, int> { [new UnitStackKey("infantry", 1)] = 10 },
                ArrivesAt, ArrivesAt.AddMinutes(-20));

        [Fact]
        public void NewMarch_ShouldHaveNoRetry()
            => Assert.Null(NewMarch().ArrivalRetryAt);

        [Fact]
        public void DeferArrival_ShouldWaitAtLeastTenSeconds_OnTheFirstFailure()
        {
            var march = NewMarch();
            var now = ArrivesAt.AddSeconds(1);

            march.DeferArrival(now);

            Assert.Equal(now.AddSeconds(10), march.ArrivalRetryAt);
        }

        /// <summary>Наступна пауза дорівнює простроченню — кожна спроба вдвічі далі за попередню.</summary>
        [Fact]
        public void DeferArrival_ShouldWaitAsLongAsTheMarchIsOverdue()
        {
            var march = NewMarch();
            var now = ArrivesAt.AddMinutes(5);

            march.DeferArrival(now);

            Assert.Equal(now.AddMinutes(5), march.ArrivalRetryAt);
        }

        [Fact]
        public void DeferArrival_ShouldCapThePauseAtAnHour()
        {
            var march = NewMarch();
            var now = ArrivesAt.AddDays(1);

            march.DeferArrival(now);

            Assert.Equal(now.AddHours(1), march.ArrivalRetryAt);
        }
    }
}
