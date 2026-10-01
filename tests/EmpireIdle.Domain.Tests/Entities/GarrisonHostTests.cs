using EmpireIdle.Domain.Entities;
using EmpireIdle.Domain.Enums;
using EmpireIdle.Domain.ValueObjects;

namespace EmpireIdle.Domain.Tests.Entities
{
    /// <summary>
    /// Гарнізон належить або селу, або клановій споруді. Сільська логіка
    /// не має тихо спрацьовувати на гарнізоні споруди.
    /// </summary>
    public class GarrisonHostTests
    {
        private const int ServerId = 1;
        private static readonly DateTime Now = new(2026, 9, 25, 12, 0, 0, DateTimeKind.Utc);

        [Fact]
        public void VillageGarrison_ShouldExposeItsVillage()
        {
            var villageId = Guid.NewGuid();

            var garrison = new Garrison(Guid.NewGuid(), villageId, ServerId);

            Assert.Equal(GarrisonHost.Village, garrison.HostKind);
            Assert.Equal(villageId, garrison.HostId);
            Assert.Equal(villageId, garrison.VillageId);
        }

        [Fact]
        public void StructureGarrison_ShouldRefuseToActAsAVillage()
        {
            var structureId = Guid.NewGuid();

            var garrison = Garrison.ForStructure(Guid.NewGuid(), structureId, ServerId);

            Assert.Equal(GarrisonHost.ClanStructure, garrison.HostKind);
            Assert.Equal(structureId, garrison.HostId);
            Assert.Throws<InvalidOperationException>(() => garrison.VillageId);
        }

        /// <summary>Своїх юнітів у споруди немає: усе, що в ній стоїть, — підкріплення членів клану.</summary>
        [Fact]
        public void StructureGarrison_ShouldHoldMembersUnitsAsReinforcements()
        {
            var garrison = Garrison.ForStructure(Guid.NewGuid(), Guid.NewGuid(), ServerId);
            var owner = Guid.NewGuid();

            garrison.AddReinforcements(owner, Guid.NewGuid(),
                new Dictionary<UnitStackKey, int> { [new UnitStackKey("infantry", 1)] = 10 }, capacity: 100, Now);

            Assert.Empty(garrison.Units);
            Assert.Equal(10, garrison.ReinforcementCount);
            Assert.Contains(owner, garrison.ReinforcementOwners());
        }
    }
}
