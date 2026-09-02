using EmpireIdle.Domain.Services;

namespace EmpireIdle.Domain.Tests.Services
{
    public class ProgressionCurvesTests
    {
        /// <summary>Вартість геометрична: рівень 1 — базова, далі множиться на growth.</summary>
        [Theory]
        [InlineData(1, 100)]   // 100 × 1.45^0
        [InlineData(2, 145)]   // 100 × 1.45^1
        [InlineData(3, 210)]   // 100 × 1.45^2 = 210.25 → 210
        [InlineData(5, 442)]   // 100 × 1.45^4 = 442.05 → 442
        public void UpgradeCost_ShouldGrowGeometrically(int currentLevel, int expected)
        {
            Assert.Equal(expected, ProgressionCurves.UpgradeCost(100, currentLevel, 1.45));
        }

        /// <summary>
        /// Обрізання замість переповнення: growth 1.45 виносить int приблизно
        /// на 55 рівні, і без клампа вартість стала б від'ємною — тобто апгрейд
        /// безкоштовним саме там, де він має бути найдорожчим.
        /// </summary>
        [Fact]
        public void UpgradeCost_ShouldClampInsteadOfOverflowing()
        {
            Assert.Equal(int.MaxValue, ProgressionCurves.UpgradeCost(1000, currentLevel: 80, growth: 1.45));
        }

        /// <summary>Growth 1.0 — вартість не росте взагалі.</summary>
        [Fact]
        public void UpgradeCost_ShouldStayFlat_WhenGrowthIsOne()
        {
            Assert.Equal(100, ProgressionCurves.UpgradeCost(100, currentLevel: 20, growth: 1.0));
        }

        /// <summary>
        /// Буфер лінійний: разом із лінійним виробітком це тримає його
        /// сталим у годинах на всіх рівнях.
        /// </summary>
        [Theory]
        [InlineData(1, 60)]
        [InlineData(10, 600)]
        [InlineData(30, 1800)]
        public void BufferCap_ShouldGrowLinearly(int level, int expected)
        {
            Assert.Equal(expected, ProgressionCurves.BufferCap(60, level));
        }

        /// <summary>Кламп на абсурдній базі з конфіга: його редагують руками.</summary>
        [Fact]
        public void BufferCap_ShouldClampInsteadOfOverflowing()
        {
            Assert.Equal(int.MaxValue, ProgressionCurves.BufferCap(int.MaxValue / 10, level: 100));
        }
    }
}
