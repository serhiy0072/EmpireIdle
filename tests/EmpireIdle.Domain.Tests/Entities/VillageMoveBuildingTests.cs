using EmpireIdle.Domain.Entities;
using EmpireIdle.Domain.Exceptions;
using EmpireIdle.Domain.Services.Config;
using EmpireIdle.TestKit;

namespace EmpireIdle.Domain.Tests.Entities
{
    /// <summary>Розстановка будівель на плані села (рішення 13.10.2026).</summary>
    public class VillageMoveBuildingTests
    {
        private static readonly DateTime Now = new(2026, 10, 13, 12, 0, 0, DateTimeKind.Utc);

        /// <summary>Ратуша посередині, ферма в кутку — як у конфігу.</summary>
        private static (Village Village, Dictionary<string, BuildingConfig> Configs, Building Farm) Arrange()
        {
            var village = TestKit.Entities.VillageWithTownhall(townhallLevel: 1, resourceAmount: 1000);
            var configs = TestKit.Entities.FarmConfigs();
            configs[TestKeys.Townhall].Position = new BuildingPosition { X = 50, Y = 50 };
            configs[TestKeys.Farm].Position = new BuildingPosition { X = 20, Y = 20 };

            return (village, configs, village.Buildings.Single(b => b.Type == TestKeys.Farm));
        }

        /// <summary>Поки гравець не рухав будівлю, вона стоїть там, де каже конфіг.</summary>
        [Fact]
        public void PositionOr_ShouldFallBackToTheConfig_UntilMoved()
        {
            var (_, configs, farm) = Arrange();

            Assert.Equal((20.0, 20.0), farm.PositionOr(configs[TestKeys.Farm].Position));
        }

        /// <summary>Вільне місце на сітці всередині муру — будівля переїжджає туди.</summary>
        [Fact]
        public void MoveBuilding_ShouldPlaceTheBuilding_OnAFreeSpot()
        {
            var (village, configs, farm) = Arrange();

            village.MoveBuilding(farm.Id, 80, 30, configs, Now);

            Assert.Equal((80.0, 30.0), farm.PositionOr(configs[TestKeys.Farm].Position));
        }

        /// <summary>Поза муром чи не на вузлі сітки — відмова з межами для клієнта.</summary>
        [Theory]
        [InlineData(10, 30)]
        [InlineData(90, 30)]
        [InlineData(30, 31)]
        public void MoveBuilding_ShouldRefuse_ASpotOffTheGridOrOutsideTheWall(double x, double y)
        {
            var (village, configs, farm) = Arrange();

            var refusal = Assert.Throws<RequirementNotMetException>(() => village.MoveBuilding(farm.Id, x, y, configs, Now));

            Assert.Equal(RefusalReasons.VillageBuildingOutOfBounds.Key, refusal.Reason);
        }

        /// <summary>Ближче за 12 до іншої будівлі за обома осями — налазить, відмова називає сусіда.</summary>
        [Fact]
        public void MoveBuilding_ShouldRefuse_ASpotThatOverlapsAnotherBuilding()
        {
            var (village, configs, farm) = Arrange();

            var refusal = Assert.Throws<RequirementNotMetException>(() => village.MoveBuilding(farm.Id, 40, 40, configs, Now));

            Assert.Equal(RefusalReasons.VillageBuildingOverlaps.Key, refusal.Reason);
            Assert.Equal((20.0, 20.0), farm.PositionOr(configs[TestKeys.Farm].Position));
        }

        /// <summary>Сусід, якого гравець уже пересунув, заважає на новому місці, а не на старому.</summary>
        [Fact]
        public void MoveBuilding_ShouldCheckOverlap_AgainstWhereOthersWereMoved()
        {
            var (village, configs, farm) = Arrange();
            var townhall = village.Buildings.Single(b => b.Type == TestKeys.Townhall);
            village.MoveBuilding(townhall.Id, 80, 80, configs, Now);

            village.MoveBuilding(farm.Id, 50, 50, configs, Now);

            Assert.Throws<RequirementNotMetException>(() => village.MoveBuilding(farm.Id, 74, 74, configs, Now));
        }

        /// <summary>Будівля на тому самому місці, де й стоїть, сама собі не заважає.</summary>
        [Fact]
        public void MoveBuilding_ShouldAllow_NudgingWithinItsOwnFootprint()
        {
            var (village, configs, farm) = Arrange();

            village.MoveBuilding(farm.Id, 22, 20, configs, Now);

            Assert.Equal((22.0, 20.0), farm.PositionOr(configs[TestKeys.Farm].Position));
        }

        /// <summary>Будівля без місця на плані (мур) не переставляється.</summary>
        [Fact]
        public void MoveBuilding_ShouldRefuse_ABuildingWithoutAPlaceOnThePlan()
        {
            var (village, configs, farm) = Arrange();
            configs[TestKeys.Farm].Position = null;

            Assert.Throws<RequirementNotMetException>(() => village.MoveBuilding(farm.Id, 80, 30, configs, Now));
        }
    }
}
