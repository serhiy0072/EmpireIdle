using EmpireIdle.Domain.Services;
using EmpireIdle.Domain.ValueObjects;

namespace EmpireIdle.Domain.Tests.Entities
{
    public class VillageTests
    {
        /// <summary>Збір перекладає накопичене в ресурси села й обнуляє буфер.</summary>
        [Fact]
        public void CollectFromBuilding_ShouldMoveBufferIntoVillageResources()
        {
            var village = TestData.CreateVillageWithResources(1000);
            var configs = TestData.FarmConfigs();

            village.AddBuilding("farm", configs);
            var building = village.Buildings.Single();

            var foodBefore = village.Resources.Single(r => r.ResourceType == "food").Amount;
            var collectAt = building.LastAccruedAt.AddMinutes(5);

            village.CollectFromBuilding(building.Id, configs, collectAt, ProductionBoost.None);

            Assert.Equal(0, building.AccruedAmount);
            Assert.Equal(foodBefore + 50, village.Resources.Single(r => r.ResourceType == "food").Amount);
        }

        /// <summary>Збір із порожнього буфера — не подія: ресурси не змінюються.</summary>
        [Fact]
        public void CollectFromBuilding_ShouldDoNothing_WhenBufferIsEmpty()
        {
            var village = TestData.CreateVillageWithResources(1000);
            var configs = TestData.FarmConfigs();

            village.AddBuilding("farm", configs);
            var building = village.Buildings.Single();

            var foodBefore = village.Resources.Single(r => r.ResourceType == "food").Amount;

            village.CollectFromBuilding(building.Id, configs, building.LastAccruedAt, ProductionBoost.None);

            Assert.Equal(foodBefore, village.Resources.Single(r => r.ResourceType == "food").Amount);
        }

        /// <summary>Додавання будівлі кладе її в колекцію з правильним VillageId.</summary>
        [Fact]
        public void AddBuilding_ShouldPlaceBuildingInVillage()
        {
            var village = TestData.CreateVillageWithResources(200);
            var configs = TestData.FarmConfigs();

            village.AddBuilding("farm", configs);

            Assert.Single(village.Buildings);
            Assert.Equal(village.Id, village.Buildings.First().VillageId);
        }

        /// <summary>Кожна будівля унікальна — другу такого ж типу поставити не можна.</summary>
        [Fact]
        public void AddBuilding_ShouldRejectDuplicateType()
        {
            var village = TestData.CreateVillageWithResources(1000);
            var configs = TestData.FarmConfigs();

            village.AddBuilding("farm", configs);

            Assert.Throws<InvalidOperationException>(() => village.AddBuilding("farm", configs));
        }

        /// <summary>Перша будівля вже не безкоштовна — вартість списується завжди.</summary>
        [Fact]
        public void AddBuilding_ShouldChargeCost()
        {
            var village = TestData.CreateVillageWithResources(200);
            var configs = TestData.FarmConfigs();

            village.AddBuilding("farm", configs);

            Assert.Equal(100, village.Resources.Single(r => r.ResourceType == "food").Amount);
        }

        /// <summary>Без ресурсів будівлю не поставити.</summary>
        [Fact]
        public void AddBuilding_ShouldReject_WhenResourcesAreInsufficient()
        {
            var village = TestData.CreateVillageWithResources(50);
            var configs = TestData.FarmConfigs();

            Assert.Throws<InvalidOperationException>(() => village.AddBuilding("farm", configs));
            Assert.Empty(village.Buildings);
        }

        /// <summary>BeginBuildingUpgrade списує ресурси й ставить будівлю в стан будівництва.</summary>
        [Fact]
        public void BeginBuildingUpgrade_ShouldChargeCostAndStartConstruction()
        {
            var village = TestData.CreateVillageWithResources(300);
            var configs = TestData.FarmConfigs();

            village.AddBuilding("farm", configs);
            var building = village.Buildings.First();

            village.BeginBuildingUpgrade(building.Id, configs, DateTime.UtcNow, ProductionBoost.None);

            Assert.True(building.IsUnderConstruction);
            Assert.NotNull(building.ConstructionCompletesAt);
            Assert.Equal(100, village.Resources.Single(r => r.ResourceType == "food").Amount);
        }

        /// <summary>Апгрейд банкує вироблене до зупинки — воно не губиться.</summary>
        [Fact]
        public void BeginBuildingUpgrade_ShouldBankProductionBeforeFreezing()
        {
            var village = TestData.CreateVillageWithResources(300);
            var configs = TestData.FarmConfigs();

            village.AddBuilding("farm", configs);
            var building = village.Buildings.First();

            village.BeginBuildingUpgrade(building.Id, configs, building.LastAccruedAt.AddMinutes(4), ProductionBoost.None);

            Assert.Equal(40, building.AccruedAmount);
        }

        /// <summary>Сканер завершує лише ті будівництва, чий час настав.</summary>
        [Fact]
        public void CompleteDueConstructions_ShouldRaiseLevelOnlyForDueBuildings()
        {
            var village = TestData.CreateVillageWithResources(300);
            var configs = TestData.FarmConfigs();

            village.AddBuilding("farm", configs);
            var building = village.Buildings.First();
            var startedAt = DateTime.UtcNow;

            village.BeginBuildingUpgrade(building.Id, configs, startedAt, ProductionBoost.None);

            Assert.Equal(0, village.CompleteDueConstructions(startedAt.AddMinutes(1), configs));
            Assert.Equal(1, building.Level.Value);

            Assert.Equal(1, village.CompleteDueConstructions(startedAt.AddMinutes(10), configs));
            Assert.Equal(2, building.Level.Value);
            Assert.False(building.IsUnderConstruction);
        }

        /// <summary>
        /// ChargeCost списує все або нічого: при нестачі одного ресурсу
        /// решта лишається недоторканою.
        /// </summary>
        [Fact]
        public void ChargeCost_ShouldNotChargeAnything_WhenOneResourceIsInsufficient()
        {
            var village = TestData.CreateVillage();
            village.Resources.Single(r => r.ResourceType == "gold").Add(100);
            village.Resources.Single(r => r.ResourceType == "food").Add(10);

            var cost = new List<ResourceCost>
            {
                new() { Resource = "gold", Amount = 10 },
                new() { Resource = "food", Amount = 50 } // не вистачає
            };

            Assert.Throws<InvalidOperationException>(() => village.ChargeCost(cost));
            Assert.Equal(100, village.Resources.Single(r => r.ResourceType == "gold").Amount);
        }

        /// <summary>Фіксація буфера перед зміною буста не втрачає вироблене.</summary>
        [Fact]
        public void MaterializeProduction_ShouldBankAccruedAmountForAllBuildings()
        {
            var village = TestData.CreateVillageWithResources(1000);
            var configs = TestData.FarmConfigs();

            village.AddBuilding("farm", configs);
            var building = village.Buildings.Single();
            var start = building.LastAccruedAt;

            // 4 хв під бустом ×1.5 = 60
            var boost = new ProductionBoost(1.5, start, start.AddHours(1));
            village.MaterializeProduction(configs, start.AddMinutes(4), boost);

            Assert.Equal(60, building.AccruedAmount);
            Assert.Equal(start.AddMinutes(4), building.LastAccruedAt);
        }
    }
}
