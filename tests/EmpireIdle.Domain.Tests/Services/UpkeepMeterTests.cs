using EmpireIdle.Domain.Services;

namespace EmpireIdle.Domain.Tests.Services
{
    /// <summary>
    /// Провізія сплачується кожні 10 хвилин: дріб мусить переходити на наступний прогін,
    /// інакше Їдальня на 15 за годину з'їдала б лише 12.
    /// </summary>
    public class UpkeepMeterTests
    {
        private static readonly DateTime Start = new(2026, 10, 13, 12, 0, 0, DateTimeKind.Utc);

        [Fact]
        public void Due_ShouldChargeTheFullHourlyRate_AcrossTenMinuteRuns()
        {
            var total = 0;

            for (var run = 0; run < 6; run++)
                total += UpkeepMeter.Due(15, Start.AddMinutes(run * 10), Start.AddMinutes((run + 1) * 10));

            Assert.Equal(15, total);
        }

        /// <summary>Темп нижче одиниці за прогін: раніше округлення вниз не списувало нічого ніколи.</summary>
        [Fact]
        public void Due_ShouldEventuallyCharge_ARateBelowOnePerRun()
        {
            var total = 0;

            for (var run = 0; run < 12; run++)
                total += UpkeepMeter.Due(5, Start.AddMinutes(run * 10), Start.AddMinutes((run + 1) * 10));

            Assert.Equal(10, total);
        }

        [Fact]
        public void Due_ShouldChargeWholeHoursExactly()
            => Assert.Equal(420, UpkeepMeter.Due(210, Start, Start.AddHours(2)));

        [Theory]
        [InlineData(0)]
        [InlineData(-3)]
        public void Due_ShouldBeZero_ForANonPositiveRate(double perHour)
            => Assert.Equal(0, UpkeepMeter.Due(perHour, Start, Start.AddHours(1)));

        /// <summary>Годинник пішов назад або прогін той самий — нічого не набігло.</summary>
        [Fact]
        public void Due_ShouldBeZero_WhenNoTimePassed()
        {
            Assert.Equal(0, UpkeepMeter.Due(15, Start, Start));
            Assert.Equal(0, UpkeepMeter.Due(15, Start, Start.AddMinutes(-10)));
        }
    }
}
