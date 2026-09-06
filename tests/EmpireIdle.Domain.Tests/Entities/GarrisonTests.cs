using EmpireIdle.Domain.Entities;
using EmpireIdle.Domain.Exceptions;

namespace EmpireIdle.Domain.Tests.Entities
{
    public class GarrisonTests
    {
        private int ServerId { get; set; } = 1;

        /// <summary>
        /// Тренування ставить замовлення в чергу з коректним часом завершення,
        /// юніти в гарнізоні ще не з'являються.
        /// </summary>
        [Fact]
        public void TrainUnits_ShouldQueueOrder_WithoutAddingUnitsImmediately()
        {
            // Arrange
            var garrison = new Garrison(Guid.NewGuid(), Guid.NewGuid(), ServerId);

            // Act
            garrison.TrainUnits("infantry", 3, 5, 100, TimeSpan.FromMinutes(6), DateTime.UtcNow);

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
            var garrison = new Garrison(Guid.NewGuid(), Guid.NewGuid(), ServerId);

            Assert.Throws<RequirementNotMetException>(() =>
                garrison.TrainUnits("infantry", count, 5, 100, TimeSpan.FromMinutes(1), DateTime.UtcNow));
        }

        /// <summary>
        /// Одночасно може тренуватись лише одна партія.
        /// </summary>
        [Fact]
        public void TrainUnits_ShouldRejectSecondOrder_WhileFirstIsActive()
        {
            var garrison = new Garrison(Guid.NewGuid(), Guid.NewGuid(), ServerId);
            garrison.TrainUnits("infantry", 2, 5, 100, TimeSpan.FromMinutes(4), DateTime.UtcNow);

            Assert.Throws<InvalidStateException>(() =>
                garrison.TrainUnits("archer", 1, 5, 100, TimeSpan.FromMinutes(2), DateTime.UtcNow));
        }

        /// <summary>
        /// Завершення дозрілого замовлення переносить юнітів у гарнізон
        /// і прибирає замовлення з черги.
        /// </summary>
        [Fact]
        public void CompleteDueTraining_ShouldMoveUnitsToGarrison()
        {
            // Arrange: замовлення на 3 юніти, що дозріє через 6 хв
            var garrison = new Garrison(Guid.NewGuid(), Guid.NewGuid(), ServerId);
            garrison.TrainUnits("infantry", 3, 5, 100, TimeSpan.FromMinutes(6), DateTime.UtcNow);

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
            var garrison = new Garrison(Guid.NewGuid(), Guid.NewGuid(), ServerId);
            garrison.TrainUnits("infantry", 2, 5, 100, TimeSpan.FromMinutes(30), DateTime.UtcNow);

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
            var garrison = new Garrison(Guid.NewGuid(), Guid.NewGuid(), ServerId);

            garrison.TrainUnits("infantry", 2, 5, 100, TimeSpan.FromMinutes(4), DateTime.UtcNow);
            garrison.CompleteDueTraining(DateTime.UtcNow.AddMinutes(5));

            garrison.TrainUnits("infantry", 3, 5, 100, TimeSpan.FromMinutes(6), DateTime.UtcNow);
            garrison.CompleteDueTraining(DateTime.UtcNow.AddMinutes(10));

            var unit = Assert.Single(garrison.Units);
            Assert.Equal(5, unit.Count); // 2 + 3
        }
        /// <summary>Відправка знімає юнітів із гарнізону.</summary>
        [Fact]
        public void SendUnits_ShouldRemoveUnitsFromGarrison()
        {
            var garrison = new Garrison(Guid.NewGuid(), Guid.NewGuid(), ServerId);
            garrison.TrainUnits("infantry", 5, 5, 100, TimeSpan.FromMinutes(10), DateTime.UtcNow);
            garrison.CompleteDueTraining(DateTime.UtcNow.AddMinutes(11));

            garrison.SendUnits(new Dictionary<string, int> { ["infantry"] = 3 }, DateTime.UtcNow);

            Assert.Equal(2, garrison.Units.Single().Count);
        }

        /// <summary>Не можна відправити більше юнітів, ніж є.</summary>
        [Fact]
        public void SendUnits_ShouldThrow_WhenNotEnoughUnits()
        {
            var garrison = new Garrison(Guid.NewGuid(), Guid.NewGuid(), ServerId);
            garrison.TrainUnits("infantry", 2, 5, 100, TimeSpan.FromMinutes(4), DateTime.UtcNow);
            garrison.CompleteDueTraining(DateTime.UtcNow.AddMinutes(5));

            Assert.Throws<NotEnoughResourcesException>(() =>
                garrison.SendUnits(new Dictionary<string, int> { ["infantry"] = 5 }, DateTime.UtcNow));

            Assert.Equal(2, garrison.Units.Single().Count); // нічого не зняли
        }

        /// <summary>Повернення армії додає юнітів назад у гарнізон.</summary>
        [Fact]
        public void ReceiveUnits_ShouldReturnUnitsToGarrison()
        {
            var garrison = new Garrison(Guid.NewGuid(), Guid.NewGuid(), ServerId);
            garrison.TrainUnits("infantry", 5, 5, 100, TimeSpan.FromMinutes(10), DateTime.UtcNow);
            garrison.CompleteDueTraining(DateTime.UtcNow.AddMinutes(11));

            var army = new Dictionary<string, int> { ["infantry"] = 3 };
            garrison.SendUnits(army, DateTime.UtcNow);
            garrison.ReceiveUnits(army, DateTime.UtcNow);

            Assert.Equal(5, garrison.Units.Single().Count); // усі повернулись
        }

        [Fact]
        public void TrainUnits_ShouldReject_WhenArmyCapacityExceeded()
        {
            var garrison = new Garrison(Guid.NewGuid(), Guid.NewGuid(), ServerId);

            garrison.TrainUnits("infantry", 5, 10, armyCapacity:6, TimeSpan.FromMinutes(10), DateTime.UtcNow);
            garrison.CompleteDueTraining(DateTime.UtcNow.AddMinutes(11));

            // 5 у гарнізоні + 2 в замовленні > 6
            Assert.Throws<RequirementNotMetException>(() =>
                garrison.TrainUnits("infantry", 2, 10, 6, TimeSpan.FromMinutes(4), DateTime.UtcNow.AddMinutes(11)));
        }

        /// <summary>
        /// Підкріплення лягають окремою колекцією й не змішуються з власною
        /// армією: ліміт у них свій, від посольства.
        /// </summary>
        [Fact]
        public void AddReinforcements_ShouldKeepAlliedUnitsSeparateFromOwnArmy()
        {
            // Arrange
            var garrison = new Garrison(Guid.NewGuid(), Guid.NewGuid(), ServerId);
            var ownerId = Guid.NewGuid();
            var ownerGarrisonId = Guid.NewGuid();
            var now = DateTime.UtcNow;

            // Act
            garrison.AddReinforcements(ownerId, ownerGarrisonId,
                new Dictionary<string, int> { ["infantry"] = 10, ["archer"] = 5 }, 100, now);

            // Assert
            Assert.Equal(15, garrison.ReinforcementCount);
            Assert.Empty(garrison.Units);
            Assert.Equal(2, garrison.Reinforcements.Count);
        }

        /// <summary>
        /// Друга партія від того самого власника додається в наявний стек,
        /// а не заводить другий рядок на ту саму пару.
        /// </summary>
        [Fact]
        public void AddReinforcements_ShouldStackByOwnerAndType()
        {
            // Arrange
            var garrison = new Garrison(Guid.NewGuid(), Guid.NewGuid(), ServerId);
            var ownerId = Guid.NewGuid();
            var ownerGarrisonId = Guid.NewGuid();
            var now = DateTime.UtcNow;

            garrison.AddReinforcements(ownerId, ownerGarrisonId,
                new Dictionary<string, int> { ["infantry"] = 10 }, 100, now);

            // Act
            garrison.AddReinforcements(ownerId, ownerGarrisonId,
                new Dictionary<string, int> { ["infantry"] = 7 }, 100, now.AddMinutes(30));

            // Assert
            var stack = Assert.Single(garrison.Reinforcements);
            Assert.Equal(17, stack.Count);
        }

        /// <summary>
        /// Двоє союзників тримаються окремими стеками: повернення адресне,
        /// і злити їх означало б утратити, кому що віддавати.
        /// </summary>
        [Fact]
        public void AddReinforcements_ShouldNotMergeDifferentOwners()
        {
            // Arrange
            var garrison = new Garrison(Guid.NewGuid(), Guid.NewGuid(), ServerId);
            var first = Guid.NewGuid();
            var second = Guid.NewGuid();
            var now = DateTime.UtcNow;

            // Act
            garrison.AddReinforcements(first, Guid.NewGuid(),
                new Dictionary<string, int> { ["infantry"] = 10 }, 100, now);
            garrison.AddReinforcements(second, Guid.NewGuid(),
                new Dictionary<string, int> { ["infantry"] = 4 }, 100, now);

            // Assert
            Assert.Equal(2, garrison.Reinforcements.Count);
            Assert.Equal(14, garrison.ReinforcementCount);
            Assert.Equal(2, garrison.ReinforcementOwners().Count);
        }

        /// <summary>Понад місткість посольства партія не приймається — цілком, не частково.</summary>
        [Fact]
        public void AddReinforcements_ShouldRejectBatchOverCapacity()
        {
            // Arrange
            var garrison = new Garrison(Guid.NewGuid(), Guid.NewGuid(), ServerId);
            var ownerId = Guid.NewGuid();
            var now = DateTime.UtcNow;

            garrison.AddReinforcements(ownerId, Guid.NewGuid(),
                new Dictionary<string, int> { ["infantry"] = 18 }, 20, now);

            // Act
            var act = () => garrison.AddReinforcements(Guid.NewGuid(), Guid.NewGuid(),
                new Dictionary<string, int> { ["archer"] = 5 }, 20, now);

            // Assert
            Assert.Throws<RequirementNotMetException>(act);
            Assert.Equal(18, garrison.ReinforcementCount);
        }

        /// <summary>Порожня партія — помилка виклику, а не мовчазний no-op.</summary>
        [Fact]
        public void AddReinforcements_ShouldRejectEmptyBatch()
        {
            // Arrange
            var garrison = new Garrison(Guid.NewGuid(), Guid.NewGuid(), ServerId);

            // Act
            var act = () => garrison.AddReinforcements(Guid.NewGuid(), Guid.NewGuid(),
                new Dictionary<string, int> { ["infantry"] = 0 }, 100, DateTime.UtcNow);

            // Assert
            Assert.Throws<RequirementNotMetException>(act);
        }

        /// <summary>
        /// Зняття забирає війська лише одного власника й повертає їх склад —
        /// саме він поїде додому маршем.
        /// </summary>
        [Fact]
        public void WithdrawReinforcements_ShouldReturnOnlyThatOwnersUnits()
        {
            // Arrange
            var garrison = new Garrison(Guid.NewGuid(), Guid.NewGuid(), ServerId);
            var leaving = Guid.NewGuid();
            var staying = Guid.NewGuid();
            var now = DateTime.UtcNow;

            garrison.AddReinforcements(leaving, Guid.NewGuid(),
                new Dictionary<string, int> { ["infantry"] = 10, ["archer"] = 3 }, 100, now);
            garrison.AddReinforcements(staying, Guid.NewGuid(),
                new Dictionary<string, int> { ["infantry"] = 6 }, 100, now);

            // Act
            var withdrawn = garrison.WithdrawReinforcements(leaving, now.AddHours(1));

            // Assert
            Assert.Equal(10, withdrawn["infantry"]);
            Assert.Equal(3, withdrawn["archer"]);
            Assert.Equal(6, garrison.ReinforcementCount);
            Assert.DoesNotContain(garrison.Reinforcements, r => r.OwnerPlayerId == leaving);
        }

        /// <summary>
        /// Зняття в того, хто нічого не тримає, дає порожній словник:
        /// автоповернення при кіку викликається для будь-кого, і кидати тут не можна.
        /// </summary>
        [Fact]
        public void WithdrawReinforcements_ShouldReturnEmpty_WhenOwnerHasNothingHere()
        {
            // Arrange
            var garrison = new Garrison(Guid.NewGuid(), Guid.NewGuid(), ServerId);

            // Act
            var withdrawn = garrison.WithdrawReinforcements(Guid.NewGuid(), DateTime.UtcNow);

            // Assert
            Assert.Empty(withdrawn);
        }

        /// <summary>
        /// Чужі підкріплення не займають ліміт власної армії: він рахується
        /// від казарм і стосується лише своїх юнітів.
        /// </summary>
        [Fact]
        public void TrainUnits_ShouldIgnoreReinforcements_WhenCheckingArmyCapacity()
        {
            // Arrange
            var garrison = new Garrison(Guid.NewGuid(), Guid.NewGuid(), ServerId);
            var now = DateTime.UtcNow;

            garrison.AddReinforcements(Guid.NewGuid(), Guid.NewGuid(),
                new Dictionary<string, int> { ["infantry"] = 50 }, 100, now);

            // Act
            garrison.TrainUnits("infantry", 3, 5, 5, TimeSpan.FromMinutes(6), now);

            // Assert
            var order = Assert.Single(garrison.TrainingOrders);
            Assert.Equal(3, order.Count);
        }
    }
}
