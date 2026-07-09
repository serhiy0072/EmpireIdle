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
            var playerId = Guid.NewGuid();
            var village = new Village(Guid.NewGuid(),playerId, "Test Village");

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

            village.AddBuilding(new Building(Guid.NewGuid(),village.Id, "farm"));
            var building = village.Buildings.Single();

            village.TickProduction(configs); // накопичуємо ресурси у буфер будівлі
            var buffered = building.StoredAmount;

            //Act
            village.CollectFromBuilding(building.Id, configs);

            // Assert
            Assert.Equal(0, building.StoredAmount);
            var food = village.Resources.Single(r=>r.ResourceType == "food"); ;
            Assert.Equal(buffered, food.Amount);

        }
    }
}
