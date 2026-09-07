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
    }
}
