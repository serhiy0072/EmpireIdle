using EmpireIdle.Domain.Entities;
using EmpireIdle.Domain.Events;
using EmpireIdle.Domain.Services;

namespace EmpireIdle.Domain.Tests.Entities
{
    public class VillageTests
    {
        /// <summary>
        /// Збір з буфера будівлі перекладає накопичене у ресурси села
        /// і обнуляє буфер будівлі.
        /// </summary>
        [Fact]
        public void CollectFromBuilding_ShouldMoveBufferIntoVillageResources()
        {
            // Arrange
            var village = TestData.CreateVillage();

            // конфіг однієї будівлі farm (яку ресурс виробляє + параметри буфера)
            var configs = new Dictionary<string, BuildingConfig>
            {
                ["farm"] = new BuildingConfig
                {
                    Key = "farm",
                    ProducesResource = "food",
                    BaseProductionPerMinute = 10,
                    BaseStorage = 60,
                    StorageGrowth = 1.5
                }
            };

            village.AddBuilding(new Building(Guid.NewGuid(), village.Id, "farm"));
            var building = village.Buildings.Single();

            village.TickProduction(configs); // накопичуємо ресурси у буфер будівлі
            var buffered = building.StoredAmount;

            //Act
            village.CollectFromBuilding(building.Id, configs);

            // Assert
            Assert.Equal(0, building.StoredAmount);
            var food = village.Resources.Single(r => r.ResourceType == "food"); ;
            Assert.Equal(buffered, food.Amount);

        }
        /// <summary>
        /// Додавання будівлі кладе її в колекцію Buildings із правильним VillageId.
        /// </summary>
        [Fact]
        public void AddBuilding_ShouldPlaxeBuildgingVillage()
        {
            var village = TestData.CreateVillage();
            var building = new Building(Guid.NewGuid(), village.Id, "farm");

            village.AddBuilding(building);

            Assert.Single(village.Buildings);
            Assert.Equal(building.Id, village.Buildings.First().Id);
            Assert.Equal(village.Id, village.Buildings.First().VillageId);

        }

        /// <summary>
        /// BeginBuildingUpgrade списує ресурси й ставить будівлю в стан будівництва.
        /// </summary>
        [Fact]
        public void BeginBuildingUpgrade_ShouldChargeCostAndStartConstruction()
        {
            var village = TestData.CreateVillage();
            var building = new Building(Guid.NewGuid(), village.Id, "farm");
            village.AddBuilding(building);

            var configs = new Dictionary<string, BuildingConfig>
            {
                ["farm"] = new BuildingConfig
                {
                    Key = "farm",
                    ProducesResource = "food",
                    CostResource = "food",
                    BaseCost = 100,
                    BaseStorage = 60,
                    StorageGrowth = 1.3,
                    BaseProductionPerMinute = 10,
                    BaseBuildMinutes = 5,
                    BuildTimeGrowth = 1.5
                }

            };

            // наповнимо ресурс, щоб було чим заплатити
            var food = village.Resources.Single(r => r.ResourceType == "food");
            food.Amount = 200;

            village.BeginBuildingUpgrade(building.Id, configs);

            Assert.True(building.IsUnderConstruction);
            Assert.NotNull(building.ConstructionCompletesAt);
            Assert.Equal(100, food.Amount); // 200 - 100 = 100
        }
    }
}
