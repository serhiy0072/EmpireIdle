using EmpireIdle.Domain.Combat;
using EmpireIdle.Domain.Entities;
using EmpireIdle.Domain.Services;

namespace EmpireIdle.Domain.Tests.Services
{
    /// <summary>
    /// Головний інваріант: сума втрат по стеках дорівнює загальній втраті
    /// рівно. Будь-яке округлення тут або створює юнітів, або з'їдає.
    /// </summary>
    public class DefenceLossAllocatorTests
    {
        private static readonly Guid Ally = Guid.NewGuid();
        private static readonly Guid SecondAlly = Guid.NewGuid();

        private readonly DefenceLossAllocator _allocator = new();

        [Fact]
        public void Allocate_ShouldSplitProportionally_WhenSharesAreExact()
        {
            // Arrange: 30 своїх і 10 союзних, втрачено 20
            var stacks = new List<DefenceStack>
            {
                new(null, "infantry", 30),
                new(Ally, "infantry", 10)
            };

            // Act
            var losses = _allocator.Allocate(stacks, new Dictionary<string, int> { ["infantry"] = 20 });

            // Assert
            Assert.Equal(15, losses.Single(l => l.OwnerPlayerId is null).Lost);
            Assert.Equal(5, losses.Single(l => l.OwnerPlayerId == Ally).Lost);
        }

        [Fact]
        public void Allocate_ShouldKeepTotalExact_WhenSharesAreFractional()
        {
            // Arrange: три рівні стеки й непарна втрата — дроби неминучі
            var stacks = new List<DefenceStack>
            {
                new(null, "archer", 10),
                new(Ally, "archer", 10),
                new(SecondAlly, "archer", 10)
            };

            // Act
            var losses = _allocator.Allocate(stacks, new Dictionary<string, int> { ["archer"] = 10 });

            // Assert
            Assert.Equal(10, losses.Sum(l => l.Lost));
        }

        [Fact]
        public void Allocate_ShouldGiveRemainderToTheLargestFraction()
        {
            // Arrange: 7 і 3, утрачено 5 → частки 3.5 і 1.5, залишок один
            var stacks = new List<DefenceStack>
            {
                new(null, "cavalry", 7),
                new(Ally, "cavalry", 3)
            };

            // Act
            var losses = _allocator.Allocate(stacks, new Dictionary<string, int> { ["cavalry"] = 5 });

            // Assert
            Assert.Equal(5, losses.Sum(l => l.Lost));
            Assert.Equal(4, losses.Single(l => l.OwnerPlayerId is null).Lost);
            Assert.Equal(1, losses.Single(l => l.OwnerPlayerId == Ally).Lost);
        }

        [Fact]
        public void Allocate_ShouldHandleEachTypeIndependently()
        {
            // Arrange
            var stacks = new List<DefenceStack>
            {
                new(null, "infantry", 20),
                new(Ally, "infantry", 20),
                new(null, "siege", 5)
            };

            // Act
            var losses = _allocator.Allocate(stacks, new Dictionary<string, int>
            {
                ["infantry"] = 10,
                ["siege"] = 5
            });

            // Assert
            Assert.Equal(5, losses.Single(l => l.UnitType == "infantry" && l.OwnerPlayerId is null).Lost);
            Assert.Equal(5, losses.Single(l => l.UnitType == "infantry" && l.OwnerPlayerId == Ally).Lost);
            Assert.Equal(5, losses.Single(l => l.UnitType == "siege").Lost);
        }

        [Fact]
        public void Allocate_ShouldNotKillMoreThanStood_WhenLossesExceedDefence()
        {
            // Arrange
            var stacks = new List<DefenceStack> { new(null, "infantry", 8) };

            // Act
            var losses = _allocator.Allocate(stacks, new Dictionary<string, int> { ["infantry"] = 99 });

            // Assert
            Assert.Equal(8, losses.Single().Lost);
        }

        [Fact]
        public void Allocate_ShouldSkipTypesWithoutLosses()
        {
            // Arrange
            var stacks = new List<DefenceStack>
            {
                new(null, "infantry", 10),
                new(null, "archer", 10)
            };

            // Act
            var losses = _allocator.Allocate(stacks, new Dictionary<string, int> { ["infantry"] = 4 });

            // Assert
            Assert.Single(losses);
            Assert.Equal("infantry", losses[0].UnitType);
        }

        [Fact]
        public void Allocate_ShouldReturnNothing_WhenDefenceIsEmpty()
        {
            // Act
            var losses = _allocator.Allocate([], new Dictionary<string, int> { ["infantry"] = 10 });

            // Assert
            Assert.Empty(losses);
        }

        /// <summary>
        /// Підсилений стек утрачає менше за того самого розміру.
        /// Це і є сенс коміту: до нього бонус піднімав силу, але не
        /// рятував від утрат, тож лідер союзника захищав кого завгодно,
        /// крім власних юнітів.
        /// </summary>
        [Fact]
        public void Allocate_ShouldSpareTheBuffedStack()
        {
            var ally = Guid.NewGuid();

            var stacks = new List<DefenceStack>
            {
                new(null, "infantry", 100),
                new(ally, "infantry", 100)
            };

            var buffs = new DefenceBuffs(
                StackBuff.None,
                new Dictionary<Guid, StackBuff> { [ally] = TestKit.Passives.Buff(passives: TestKit.Passives.Defence(100)) });

            var losses = _allocator.Allocate(stacks, new Dictionary<string, int> { ["infantry"] = 90 }, buffs);

            var host = losses.Single(l => l.OwnerPlayerId is null).Lost;
            var allied = losses.Single(l => l.OwnerPlayerId == ally).Lost;

            // Подвоєний захист — удвічі менша частка: 60 проти 30
            Assert.Equal(60, host);
            Assert.Equal(30, allied);
            Assert.Equal(90, host + allied);
        }

        /// <summary>Сума втрат по стеках дорівнює загальній за будь-яких бонусів.</summary>
        [Theory]
        [InlineData(1)]
        [InlineData(7)]
        [InlineData(99)]
        [InlineData(150)]
        public void Allocate_ShouldConserveTheTotal(int lost)
        {
            var ally = Guid.NewGuid();

            var stacks = new List<DefenceStack>
            {
                new(null, "infantry", 100),
                new(ally, "infantry", 60)
            };

            var buffs = new DefenceBuffs(
                StackBuff.None,
                new Dictionary<Guid, StackBuff> { [ally] = TestKit.Passives.Buff(passives: TestKit.Passives.Defence(40)) });

            var losses = _allocator.Allocate(stacks, new Dictionary<string, int> { ["infantry"] = lost }, buffs);

            Assert.Equal(Math.Min(lost, 160), losses.Sum(l => l.Lost));
        }

        /// <summary>
        /// Жоден стек не втрачає більше, ніж має. З вагами це вже не
        /// випливає з арифметики: сильна пасивка в сусіда зсуває частку
        /// на слабкого понад його розмір.
        /// </summary>
        [Fact]
        public void Allocate_ShouldNeverExceedAStackSize()
        {
            var ally = Guid.NewGuid();

            var stacks = new List<DefenceStack>
            {
                new(null, "infantry", 10),
                new(ally, "infantry", 200)
            };

            var buffs = new DefenceBuffs(
                StackBuff.None,
                new Dictionary<Guid, StackBuff> { [ally] = TestKit.Passives.Buff(passives: TestKit.Passives.Defence(300)) });

            var losses = _allocator.Allocate(stacks, new Dictionary<string, int> { ["infantry"] = 150 }, buffs);

            Assert.True(losses.Single(l => l.OwnerPlayerId is null).Lost <= 10);
            Assert.Equal(150, losses.Sum(l => l.Lost));
        }

        /// <summary>Без пасивок поділ той самий, що й до героїв.</summary>
        [Fact]
        public void Allocate_ShouldFallBackToCountShares_WhenThereAreNoBuffs()
        {
            var ally = Guid.NewGuid();

            var stacks = new List<DefenceStack>
            {
                new(null, "infantry", 75),
                new(ally, "infantry", 25)
            };

            var losses = _allocator.Allocate(stacks, new Dictionary<string, int> { ["infantry"] = 40 });

            Assert.Equal(30, losses.Single(l => l.OwnerPlayerId is null).Lost);
            Assert.Equal(10, losses.Single(l => l.OwnerPlayerId == ally).Lost);
        }
    }
}
