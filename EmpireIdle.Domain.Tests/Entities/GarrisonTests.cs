using EmpireIdle.Domain.Entities;

namespace EmpireIdle.Domain.Tests.Entities
{
    public class GarrisonTests
    {
        /// <summary>
        /// Тренування ставить замовлення в чергу з коректним часом завершення,
        /// юніти в гарнізоні ще не з'являються.
        /// </summary>
        [Fact]
        public void TrainUnits_ShouldQueueOrder_WithoutAddingUnitsImmediately()
        {
            // Arrange
            var garrison = new Garrison(Guid.NewGuid(), Guid.NewGuid());

            // Act
            garrison.TrainUnits("infantry", 3, 5, TimeSpan.FromMinutes(6), DateTime.UtcNow);

            // Assert
            var order = Assert.Single(garrison.TrainingOrders);
            Assert.Equal("infantry", order.UnitType);
            Assert.Equal(3, order.Count);
            Assert.Empty(garrison.Units); // юніти приходять лише після завершення
        }

        /// <summary>
        /// Розмір партії обмежений 1–5: за межами діапазону — виняток.
        /// </summary>
        [Theory]
        [InlineData(0)]
        [InlineData(6)]
        public void TrainUnits_ShouldRejectInvalidBatchSize(int count)
        {
            var garrison = new Garrison(Guid.NewGuid(), Guid.NewGuid());

            Assert.Throws<InvalidOperationException>(() =>
                garrison.TrainUnits("infantry", count, 5, TimeSpan.FromMinutes(1), DateTime.UtcNow));
        }

        /// <summary>
        /// Одночасно може тренуватись лише одна партія.
        /// </summary>
        [Fact]
        public void TrainUnits_ShouldRejectSecondOrder_WhileFirstIsActive()
        {
            var garrison = new Garrison(Guid.NewGuid(), Guid.NewGuid());
            garrison.TrainUnits("infantry", 2, 5, TimeSpan.FromMinutes(4), DateTime.UtcNow);

            Assert.Throws<InvalidOperationException>(() =>
                garrison.TrainUnits("archer", 1, 5, TimeSpan.FromMinutes(2), DateTime.UtcNow));
        }

        /// <summary>
        /// Завершення дозрілого замовлення переносить юнітів у гарнізон
        /// і прибирає замовлення з черги.
        /// </summary>
        [Fact]
        public void CompleteDueTraining_ShouldMoveUnitsToGarrison()
        {
            // Arrange: замовлення на 3 юніти, що дозріє через 6 хв
            var garrison = new Garrison(Guid.NewGuid(), Guid.NewGuid());
            garrison.TrainUnits("infantry", 3, 5, TimeSpan.FromMinutes(6), DateTime.UtcNow);

            // Act: сканер приходить через 10 хв — час минув
            var completed = garrison.CompleteDueTraining(DateTime.UtcNow.AddMinutes(10));

            // Assert
            Assert.Equal(1, completed);
            Assert.Empty(garrison.TrainingOrders);
            var unit = Assert.Single(garrison.Units);
            Assert.Equal("infantry", unit.UnitType);
            Assert.Equal(3, unit.Count);
        }

        /// <summary>
        /// Замовлення, чий час ще не настав, не завершується.
        /// </summary>
        [Fact]
        public void CompleteDueTraining_ShouldIgnoreOrder_WhenTimeNotReached()
        {
            var garrison = new Garrison(Guid.NewGuid(), Guid.NewGuid());
            garrison.TrainUnits("infantry", 2, 5, TimeSpan.FromMinutes(30), DateTime.UtcNow);

            var completed = garrison.CompleteDueTraining(DateTime.UtcNow.AddMinutes(5));

            Assert.Equal(0, completed);
            Assert.Single(garrison.TrainingOrders);
            Assert.Empty(garrison.Units);
        }

        /// <summary>
        /// Повторне тренування того самого типу додається до наявного стека,
        /// а не створює другий запис.
        /// </summary>
        [Fact]
        public void CompleteDueTraining_ShouldStackUnitsOfSameType()
        {
            var garrison = new Garrison(Guid.NewGuid(), Guid.NewGuid());

            garrison.TrainUnits("infantry", 2, 5, TimeSpan.FromMinutes(4), DateTime.UtcNow);
            garrison.CompleteDueTraining(DateTime.UtcNow.AddMinutes(5));

            garrison.TrainUnits("infantry", 3, 5, TimeSpan.FromMinutes(6), DateTime.UtcNow);
            garrison.CompleteDueTraining(DateTime.UtcNow.AddMinutes(10));

            var unit = Assert.Single(garrison.Units);
            Assert.Equal(5, unit.Count); // 2 + 3
        }
        /// <summary>Відправка знімає юнітів із гарнізону.</summary>
        [Fact]
        public void SendUnits_ShouldRemoveUnitsFromGarrison()
        {
            var garrison = new Garrison(Guid.NewGuid(), Guid.NewGuid());
            garrison.TrainUnits("infantry", 5, 5, TimeSpan.FromMinutes(10), DateTime.UtcNow);
            garrison.CompleteDueTraining(DateTime.UtcNow.AddMinutes(11));

            garrison.SendUnits(new Dictionary<string, int> { ["infantry"] = 3 });

            Assert.Equal(2, garrison.Units.Single().Count);
        }

        /// <summary>Не можна відправити більше юнітів, ніж є.</summary>
        [Fact]
        public void SendUnits_ShouldThrow_WhenNotEnoughUnits()
        {
            var garrison = new Garrison(Guid.NewGuid(), Guid.NewGuid());
            garrison.TrainUnits("infantry", 2, 5, TimeSpan.FromMinutes(4), DateTime.UtcNow);
            garrison.CompleteDueTraining(DateTime.UtcNow.AddMinutes(5));

            Assert.Throws<InvalidOperationException>(() =>
                garrison.SendUnits(new Dictionary<string, int> { ["infantry"] = 5 }));

            Assert.Equal(2, garrison.Units.Single().Count); // нічого не зняли
        }

        /// <summary>Повернення армії додає юнітів назад у гарнізон.</summary>
        [Fact]
        public void ReceiveUnits_ShouldReturnUnitsToGarrison()
        {
            var garrison = new Garrison(Guid.NewGuid(), Guid.NewGuid());
            garrison.TrainUnits("infantry", 5, 5, TimeSpan.FromMinutes(10), DateTime.UtcNow);
            garrison.CompleteDueTraining(DateTime.UtcNow.AddMinutes(11));

            var army = new Dictionary<string, int> { ["infantry"] = 3 };
            garrison.SendUnits(army);
            garrison.ReceiveUnits(army);

            Assert.Equal(5, garrison.Units.Single().Count); // усі повернулись
        }
    }
}