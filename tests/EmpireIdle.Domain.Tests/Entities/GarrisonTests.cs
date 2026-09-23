using EmpireIdle.Domain.Combat;
using EmpireIdle.Domain.Entities;
using EmpireIdle.Domain.Events;
using EmpireIdle.Domain.Exceptions;
using EmpireIdle.Domain.Services;
using EmpireIdle.Domain.ValueObjects;

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
            garrison.TrainUnits("infantry", 1, 3, 5, 100, TimeSpan.FromMinutes(6), DateTime.UtcNow);

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

            var refusal = Assert.Throws<RequirementNotMetException>(() =>
                garrison.TrainUnits("infantry", 1, count, 5, 100, TimeSpan.FromMinutes(1), DateTime.UtcNow));
            Assert.Equal(RefusalReasons.GarrisonBatchSize.Key, refusal.Reason);
        }

        /// <summary>
        /// Одночасно може тренуватись лише одна партія.
        /// </summary>
        [Fact]
        public void TrainUnits_ShouldRejectSecondOrder_WhileFirstIsActive()
        {
            var garrison = new Garrison(Guid.NewGuid(), Guid.NewGuid(), ServerId);
            garrison.TrainUnits("infantry", 1, 2, 5, 100, TimeSpan.FromMinutes(4), DateTime.UtcNow);

            var refusal = Assert.Throws<InvalidStateException>(() =>
                garrison.TrainUnits("archer", 1, 1, 5, 100, TimeSpan.FromMinutes(2), DateTime.UtcNow));
            Assert.Equal(RefusalReasons.GarrisonTrainingBusy.Key, refusal.Reason);
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
            garrison.TrainUnits("infantry", 1, 3, 5, 100, TimeSpan.FromMinutes(6), DateTime.UtcNow);

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
            garrison.TrainUnits("infantry", 1, 2, 5, 100, TimeSpan.FromMinutes(30), DateTime.UtcNow);

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

            garrison.TrainUnits("infantry", 1, 2, 5, 100, TimeSpan.FromMinutes(4), DateTime.UtcNow);
            garrison.CompleteDueTraining(DateTime.UtcNow.AddMinutes(5));

            garrison.TrainUnits("infantry", 1, 3, 5, 100, TimeSpan.FromMinutes(6), DateTime.UtcNow);
            garrison.CompleteDueTraining(DateTime.UtcNow.AddMinutes(10));

            var unit = Assert.Single(garrison.Units);
            Assert.Equal(5, unit.Count); // 2 + 3
        }
        /// <summary>Відправка знімає юнітів із гарнізону.</summary>
        [Fact]
        public void SendUnits_ShouldRemoveUnitsFromGarrison()
        {
            var garrison = new Garrison(Guid.NewGuid(), Guid.NewGuid(), ServerId);
            garrison.TrainUnits("infantry", 1, 5, 5, 100, TimeSpan.FromMinutes(10), DateTime.UtcNow);
            garrison.CompleteDueTraining(DateTime.UtcNow.AddMinutes(11));

            garrison.SendUnits(new Dictionary<UnitStackKey, int> { [new UnitStackKey("infantry", 1)] = 3 }, DateTime.UtcNow);

            Assert.Equal(2, garrison.Units.Single().Count);
        }

        /// <summary>Не можна відправити більше юнітів, ніж є.</summary>
        [Fact]
        public void SendUnits_ShouldThrow_WhenNotEnoughUnits()
        {
            var garrison = new Garrison(Guid.NewGuid(), Guid.NewGuid(), ServerId);
            garrison.TrainUnits("infantry", 1, 2, 5, 100, TimeSpan.FromMinutes(4), DateTime.UtcNow);
            garrison.CompleteDueTraining(DateTime.UtcNow.AddMinutes(5));

            var refusal = Assert.Throws<RequirementNotMetException>(() =>
                garrison.SendUnits(new Dictionary<UnitStackKey, int> { [new UnitStackKey("infantry", 1)] = 5 }, DateTime.UtcNow));
            Assert.Equal(RefusalReasons.GarrisonNotEnoughUnits.Key, refusal.Reason);
            Assert.Equal(2, refusal.Args["have"]);

            Assert.Equal(2, garrison.Units.Single().Count); // нічого не зняли
        }

        /// <summary>Повернення армії додає юнітів назад у гарнізон.</summary>
        [Fact]
        public void ReceiveUnits_ShouldReturnUnitsToGarrison()
        {
            var garrison = new Garrison(Guid.NewGuid(), Guid.NewGuid(), ServerId);
            garrison.TrainUnits("infantry", 1, 5, 5, 100, TimeSpan.FromMinutes(10), DateTime.UtcNow);
            garrison.CompleteDueTraining(DateTime.UtcNow.AddMinutes(11));

            var army = new Dictionary<UnitStackKey, int> { [new UnitStackKey("infantry", 1)] = 3 };
            garrison.SendUnits(army, DateTime.UtcNow);
            garrison.ReceiveUnits(army, DateTime.UtcNow);

            Assert.Equal(5, garrison.Units.Single().Count); // усі повернулись
        }

        [Fact]
        public void TrainUnits_ShouldReject_WhenArmyCapacityExceeded()
        {
            var garrison = new Garrison(Guid.NewGuid(), Guid.NewGuid(), ServerId);

            garrison.TrainUnits("infantry", 1, 5, 10, armyCapacity:6, TimeSpan.FromMinutes(10), DateTime.UtcNow);
            garrison.CompleteDueTraining(DateTime.UtcNow.AddMinutes(11));

            // 5 у гарнізоні + 2 в замовленні > 6
            var refusal = Assert.Throws<RequirementNotMetException>(() =>
                garrison.TrainUnits("infantry", 1, 2, 10, 6, TimeSpan.FromMinutes(4), DateTime.UtcNow.AddMinutes(11)));
            Assert.Equal(RefusalReasons.GarrisonArmyCapacity.Key, refusal.Reason);
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
                new Dictionary<UnitStackKey, int> { [new UnitStackKey("infantry", 1)] = 10, [new UnitStackKey("archer", 1)] = 5 }, 100, now);

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
                new Dictionary<UnitStackKey, int> { [new UnitStackKey("infantry", 1)] = 10 }, 100, now);

            // Act
            garrison.AddReinforcements(ownerId, ownerGarrisonId,
                new Dictionary<UnitStackKey, int> { [new UnitStackKey("infantry", 1)] = 7 }, 100, now.AddMinutes(30));

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
                new Dictionary<UnitStackKey, int> { [new UnitStackKey("infantry", 1)] = 10 }, 100, now);
            garrison.AddReinforcements(second, Guid.NewGuid(),
                new Dictionary<UnitStackKey, int> { [new UnitStackKey("infantry", 1)] = 4 }, 100, now);

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
                new Dictionary<UnitStackKey, int> { [new UnitStackKey("infantry", 1)] = 18 }, 20, now);

            // Act
            var act = () => garrison.AddReinforcements(Guid.NewGuid(), Guid.NewGuid(),
                new Dictionary<UnitStackKey, int> { [new UnitStackKey("archer", 1)] = 5 }, 20, now);

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
                new Dictionary<UnitStackKey, int> { [new UnitStackKey("infantry", 1)] = 0 }, 100, DateTime.UtcNow);

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
                new Dictionary<UnitStackKey, int> { [new UnitStackKey("infantry", 1)] = 10, [new UnitStackKey("archer", 1)] = 3 }, 100, now);
            garrison.AddReinforcements(staying, Guid.NewGuid(),
                new Dictionary<UnitStackKey, int> { [new UnitStackKey("infantry", 1)] = 6 }, 100, now);

            // Act
            var withdrawn = garrison.WithdrawReinforcements(leaving, now.AddHours(1));

            // Assert
            Assert.Equal(10, withdrawn[new UnitStackKey("infantry", 1)]);
            Assert.Equal(3, withdrawn[new UnitStackKey("archer", 1)]);
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
                new Dictionary<UnitStackKey, int> { [new UnitStackKey("infantry", 1)] = 50 }, 100, now);

            // Act
            garrison.TrainUnits("infantry", 1, 3, 5, 5, TimeSpan.FromMinutes(6), now);

            // Assert
            var order = Assert.Single(garrison.TrainingOrders);
            Assert.Equal(3, order.Count);
        }

        // ---------- Прокачка ----------

        /// <summary>
        /// Прокачка знімає юнітів із гарнізону одразу, партія стає в чергу,
        /// на новому рівні юніти ще не з'явились.
        /// </summary>
        [Fact]
        public void LevelUpUnits_ShouldRemoveUnitsImmediately_AndQueueTheOrder()
        {
            var garrison = new Garrison(Guid.NewGuid(), Guid.NewGuid(), ServerId);
            var now = DateTime.UtcNow;

            garrison.TrainUnits("infantry", 1, 10, 100, 100, TimeSpan.Zero, now);
            garrison.CompleteDueTraining(now);

            garrison.LevelUpUnits("infantry", fromLevel: 1, toLevel: 2, count: 4, maxBatchSize: 10,
                TimeSpan.FromMinutes(10), now);

            var order = Assert.Single(garrison.LevelUpOrders);
            Assert.Equal("infantry", order.UnitType);
            Assert.Equal(1, order.FromLevel);
            Assert.Equal(2, order.ToLevel);
            Assert.Equal(4, order.Count);

            // 4 знято на прокачку, 6 лишилось у гарнізоні на старому рівні
            Assert.Equal(6, garrison.Units.Single(u => u.Level == 1).Count);
        }

        /// <summary>Розмір партії прокачки обмежений maxBatchSize (10, §5.2 GDD).</summary>
        [Theory]
        [InlineData(0)]
        [InlineData(11)]
        public void LevelUpUnits_ShouldRejectInvalidBatchSize(int count)
        {
            var garrison = new Garrison(Guid.NewGuid(), Guid.NewGuid(), ServerId);
            var now = DateTime.UtcNow;

            garrison.TrainUnits("infantry", 1, 20, 100, 100, TimeSpan.Zero, now);
            garrison.CompleteDueTraining(now);

            var refusal = Assert.Throws<RequirementNotMetException>(() =>
                garrison.LevelUpUnits("infantry", 1, 2, count, 10, TimeSpan.FromMinutes(1), now));
            Assert.Equal(RefusalReasons.GarrisonBatchSize.Key, refusal.Reason);
        }

        /// <summary>Цільовий рівень має бути вищим за поточний.</summary>
        [Fact]
        public void LevelUpUnits_ShouldReject_WhenTargetLevelIsNotHigher()
        {
            var garrison = new Garrison(Guid.NewGuid(), Guid.NewGuid(), ServerId);
            var now = DateTime.UtcNow;

            garrison.TrainUnits("infantry", 2, 5, 100, 100, TimeSpan.Zero, now);
            garrison.CompleteDueTraining(now);

            var refusal = Assert.Throws<RequirementNotMetException>(() =>
                garrison.LevelUpUnits("infantry", 2, 2, 1, 10, TimeSpan.FromMinutes(1), now));
            Assert.Null(refusal.Reason);
        }

        /// <summary>Одночасно прокачується лише одна партія.</summary>
        [Fact]
        public void LevelUpUnits_ShouldRejectSecondOrder_WhileFirstIsActive()
        {
            var garrison = new Garrison(Guid.NewGuid(), Guid.NewGuid(), ServerId);
            var now = DateTime.UtcNow;

            garrison.TrainUnits("infantry", 1, 10, 100, 100, TimeSpan.Zero, now);
            garrison.CompleteDueTraining(now);

            garrison.LevelUpUnits("infantry", 1, 2, 4, 10, TimeSpan.FromMinutes(10), now);

            var refusal = Assert.Throws<InvalidStateException>(() =>
                garrison.LevelUpUnits("infantry", 1, 2, 2, 10, TimeSpan.FromMinutes(5), now));
            Assert.Equal(RefusalReasons.GarrisonLevelUpBusy.Key, refusal.Reason);
        }

        /// <summary>Не можна прокачати більше юнітів, ніж стоїть у стеку заданого рівня.</summary>
        [Fact]
        public void LevelUpUnits_ShouldThrow_WhenStackIsTooSmall()
        {
            var garrison = new Garrison(Guid.NewGuid(), Guid.NewGuid(), ServerId);
            var now = DateTime.UtcNow;

            garrison.TrainUnits("infantry", 1, 3, 100, 100, TimeSpan.Zero, now);
            garrison.CompleteDueTraining(now);

            var refusal = Assert.Throws<RequirementNotMetException>(() =>
                garrison.LevelUpUnits("infantry", 1, 2, 5, 10, TimeSpan.FromMinutes(1), now));
            Assert.Equal(RefusalReasons.GarrisonNotEnoughUnits.Key, refusal.Reason);
            Assert.Equal(5, refusal.Args["need"]);
            Assert.Equal(3, refusal.Args["have"]);

            Assert.Equal(3, garrison.Units.Single().Count); // нічого не зняли
        }

        /// <summary>Завершена прокачка повертає юнітів у гарнізон уже на новому рівні.</summary>
        [Fact]
        public void CompleteDueLevelUps_ShouldReturnUnitsAtTheNewLevel()
        {
            var garrison = new Garrison(Guid.NewGuid(), Guid.NewGuid(), ServerId);
            var now = DateTime.UtcNow;

            garrison.TrainUnits("infantry", 1, 10, 100, 100, TimeSpan.Zero, now);
            garrison.CompleteDueTraining(now);

            garrison.LevelUpUnits("infantry", 1, 2, 4, 10, TimeSpan.FromMinutes(10), now);

            var completed = garrison.CompleteDueLevelUps(now.AddMinutes(11));

            Assert.Equal(1, completed);
            Assert.Empty(garrison.LevelUpOrders);
            Assert.Equal(6, garrison.Units.Single(u => u.Level == 1).Count);
            Assert.Equal(4, garrison.Units.Single(u => u.Level == 2).Count);
        }

        /// <summary>Прокачка, чий час ще не настав, не завершується.</summary>
        [Fact]
        public void CompleteDueLevelUps_ShouldIgnoreOrder_WhenTimeNotReached()
        {
            var garrison = new Garrison(Guid.NewGuid(), Guid.NewGuid(), ServerId);
            var now = DateTime.UtcNow;

            garrison.TrainUnits("infantry", 1, 10, 100, 100, TimeSpan.Zero, now);
            garrison.CompleteDueTraining(now);

            garrison.LevelUpUnits("infantry", 1, 2, 4, 10, TimeSpan.FromMinutes(30), now);

            var completed = garrison.CompleteDueLevelUps(now.AddMinutes(5));

            Assert.Equal(0, completed);
            Assert.Single(garrison.LevelUpOrders);
            Assert.DoesNotContain(garrison.Units, u => u.Level == 2);
        }

        /// <summary>Прискорення прокачки зсуває час завершення (speedup за gems).</summary>
        [Fact]
        public void ReduceLevelUpTime_ShouldMoveCompletionEarlier()
        {
            var garrison = new Garrison(Guid.NewGuid(), Guid.NewGuid(), ServerId);
            var now = DateTime.UtcNow;

            garrison.TrainUnits("infantry", 1, 10, 100, 100, TimeSpan.Zero, now);
            garrison.CompleteDueTraining(now);

            garrison.LevelUpUnits("infantry", 1, 2, 4, 10, TimeSpan.FromMinutes(30), now);
            var order = garrison.LevelUpOrders.Single();

            garrison.ReduceLevelUpTime(order.Id, TimeSpan.FromMinutes(30), now);

            var completed = garrison.CompleteDueLevelUps(now);
            Assert.Equal(1, completed);
        }

        /// <summary>Оборона — це свої юніти плюс підкріплення, окремими стеками.</summary>
        [Fact]
        public void GetDefence_ShouldIncludeOwnUnitsAndReinforcements()
        {
            // Arrange
            var garrison = new Garrison(Guid.NewGuid(), Guid.NewGuid(), ServerId);
            var ally = Guid.NewGuid();
            var now = DateTime.UtcNow;

            garrison.TrainUnits("infantry", 1, 10, 100, 100, TimeSpan.Zero, now);
            garrison.CompleteDueTraining(now);

            garrison.AddReinforcements(ally, Guid.NewGuid(),
                new Dictionary<UnitStackKey, int> { [new UnitStackKey("infantry", 1)] = 4 }, 100, now);

            // Act
            var defence = garrison.GetDefence();

            // Assert
            Assert.Equal(2, defence.Count);
            Assert.Contains(defence, s => s.OwnerPlayerId is null && s.Count == 10);
            Assert.Contains(defence, s => s.OwnerPlayerId == ally && s.Count == 4);
        }

        /// <summary>Поранені не стоять в обороні — вони в госпіталі.</summary>
        [Fact]
        public void GetDefence_ShouldExcludeWounded()
        {
            // Arrange
            var garrison = new Garrison(Guid.NewGuid(), Guid.NewGuid(), ServerId);
            var now = DateTime.UtcNow;

            garrison.AdmitWounded(new Dictionary<UnitStackKey, int> { [new UnitStackKey("infantry", 1)] = 6 }, now);

            // Act
            var defence = garrison.GetDefence();

            // Assert
            Assert.Empty(defence);
        }

        // ---------- Події про рух підкріплень ----------

        /// <summary>
        /// Прибуття підкріплення міняє армію власника, а відбувається в чужому
        /// агрегаті. Без події його Power оновилась би лише випадково —
        /// наступним боєм чи тренуванням у себе вдома.
        /// </summary>
        [Fact]
        public void AddReinforcements_ShouldRaiseMovedEventForTheOwnerGarrison()
        {
            var garrison = new Garrison(Guid.NewGuid(), Guid.NewGuid(), ServerId);
            var ownerGarrisonId = Guid.NewGuid();

            garrison.AddReinforcements(Guid.NewGuid(), ownerGarrisonId,
                new Dictionary<UnitStackKey, int> { [new UnitStackKey("infantry", 1)] = 5 }, 100, DateTime.UtcNow);

            var moved = Assert.Single(garrison.DomainEvents.OfType<ReinforcementsMoved>());

            Assert.Equal(ownerGarrisonId, moved.OwnerGarrisonId);
        }

        /// <summary>Відкликання — той самий рух у зворотний бік.</summary>
        [Fact]
        public void WithdrawReinforcements_ShouldRaiseMovedEventForTheOwnerGarrison()
        {
            var garrison = new Garrison(Guid.NewGuid(), Guid.NewGuid(), ServerId);
            var ownerId = Guid.NewGuid();
            var ownerGarrisonId = Guid.NewGuid();

            garrison.AddReinforcements(ownerId, ownerGarrisonId,
                new Dictionary<UnitStackKey, int> { [new UnitStackKey("infantry", 1)] = 5 }, 100, DateTime.UtcNow);

            garrison.ClearDomainEvents();

            garrison.WithdrawReinforcements(ownerId, DateTime.UtcNow);

            var moved = Assert.Single(garrison.DomainEvents.OfType<ReinforcementsMoved>());

            Assert.Equal(ownerGarrisonId, moved.OwnerGarrisonId);
        }

        /// <summary>Нічого відкликати — нічого й повідомляти.</summary>
        [Fact]
        public void WithdrawReinforcements_ShouldNotRaiseEvent_WhenNothingIsDeployed()
        {
            var garrison = new Garrison(Guid.NewGuid(), Guid.NewGuid(), ServerId);

            garrison.WithdrawReinforcements(Guid.NewGuid(), DateTime.UtcNow);

            Assert.Empty(garrison.DomainEvents.OfType<ReinforcementsMoved>());
        }

        /// <summary>
        /// Полеглі підкріплення теж зменшують армію власника, тож бій
        /// в обороні має повідомити кожного постраждалого союзника окремо.
        /// </summary>
        [Fact]
        public void ApplyDefenceLosses_ShouldRaiseEventPerAffectedOwner()
        {
            var garrison = new Garrison(Guid.NewGuid(), Guid.NewGuid(), ServerId);

            var firstOwner = Guid.NewGuid();
            var secondOwner = Guid.NewGuid();
            var firstGarrison = Guid.NewGuid();
            var secondGarrison = Guid.NewGuid();

            garrison.AddReinforcements(firstOwner, firstGarrison,
                new Dictionary<UnitStackKey, int> { [new UnitStackKey("infantry", 1)] = 10 }, 100, DateTime.UtcNow);
            garrison.AddReinforcements(secondOwner, secondGarrison,
                new Dictionary<UnitStackKey, int> { [new UnitStackKey("infantry", 1)] = 10 }, 100, DateTime.UtcNow);

            garrison.ClearDomainEvents();

            garrison.ApplyDefenceLosses(
            [
                new StackLoss(firstOwner, "infantry", 1, 3),
                new StackLoss(secondOwner, "infantry", 1, 4)
            ], DateTime.UtcNow);

            var owners = garrison.DomainEvents.OfType<ReinforcementsMoved>()
                .Select(e => e.OwnerGarrisonId)
                .ToList();

            Assert.Equal(2, owners.Count);
            Assert.Contains(firstGarrison, owners);
            Assert.Contains(secondGarrison, owners);
        }

        /// <summary>
        /// Втрати самого господаря союзників не стосуються — зайва подія
        /// змусила б перераховувати Power тим, у кого нічого не змінилось.
        /// </summary>
        [Fact]
        public void ApplyDefenceLosses_ShouldNotRaiseEvent_ForOwnUnitsOnly()
        {
            var garrison = new Garrison(Guid.NewGuid(), Guid.NewGuid(), ServerId);
            garrison.ReceiveUnits(new Dictionary<UnitStackKey, int> { [new UnitStackKey("infantry", 1)] = 10 }, DateTime.UtcNow);
            garrison.ClearDomainEvents();

            garrison.ApplyDefenceLosses([new StackLoss(null, "infantry", 1, 4)], DateTime.UtcNow);

            Assert.Empty(garrison.DomainEvents.OfType<ReinforcementsMoved>());
        }

    }
}
