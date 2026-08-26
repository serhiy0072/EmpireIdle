using EmpireIdle.Domain.Exceptions;
using EmpireIdle.Domain.Services;
using EmpireIdle.Domain.ValueObjects;

namespace EmpireIdle.Domain.Tests.Entities
{
    public class VillageTests
    {
        /// <summary>Рівень сервера, що свідомо не гейтить: ці тести не про тіри.</summary>
        private const int UngatedServerLevel = 99;

        private const int LevelsPerTier = 10;

        /// <summary>Збір перекладає накопичене в ресурси села й обнуляє буфер.</summary>
        [Fact]
        public void CollectFromBuilding_ShouldMoveBufferIntoVillageResources()
        {
            var village = TestData.CreateVillageWithResources(1000);
            var configs = TestData.FarmConfigs();

            village.AddBuilding("farm", configs, DateTime.UtcNow);
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

            village.AddBuilding("farm", configs, DateTime.UtcNow);
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

            village.AddBuilding("farm", configs, DateTime.UtcNow);

            Assert.Single(village.Buildings);
            Assert.Equal(village.Id, village.Buildings.First().VillageId);
        }

        /// <summary>Кожна будівля унікальна — другу такого ж типу поставити не можна.</summary>
        [Fact]
        public void AddBuilding_ShouldRejectDuplicateType()
        {
            var village = TestData.CreateVillageWithResources(1000);
            var configs = TestData.FarmConfigs();

            village.AddBuilding("farm", configs, DateTime.UtcNow);

            Assert.Throws<AlreadyExistsException>(() => village.AddBuilding("farm", configs, DateTime.UtcNow));
        }

        /// <summary>
        /// Апгрейд списує вартість за геометричною кривою й ставить будівлю
        /// в стан будівництва. Ферма 1 рівня: 100 × 1.45^0 = 100.
        /// </summary>
        [Fact]
        public void BeginBuildingUpgrade_ShouldChargeCostAndStartConstruction()
        {
            var village = TestData.CreateVillageWithTownhall(resourceAmount: 300);
            var configs = TestData.FarmConfigs();
            var farm = village.Buildings.Single(b => b.Type == "farm");
            var now = DateTime.UtcNow;

            var foodBefore = village.Resources.Single(r => r.ResourceType == "food").Amount;

            village.BeginBuildingUpgrade(farm.Id, configs, now, ProductionBoost.None,
                mainBuildingKey: "townhall", serverLevel: UngatedServerLevel, levelsPerTier: LevelsPerTier);

            Assert.True(farm.IsUnderConstruction);
            Assert.NotNull(farm.ConstructionCompletesAt);
            Assert.Equal(foodBefore - 100, village.Resources.Single(r => r.ResourceType == "food").Amount);
        }

        /// <summary>Апгрейд банкує вироблене до зупинки — воно не губиться.</summary>
        [Fact]
        public void BeginBuildingUpgrade_ShouldBankProductionBeforeFreezing()
        {
            var village = TestData.CreateVillageWithTownhall();
            var configs = TestData.FarmConfigs();
            var farm = village.Buildings.Single(b => b.Type == "farm");

            village.BeginBuildingUpgrade(farm.Id, configs, farm.LastAccruedAt.AddMinutes(4), ProductionBoost.None,
                mainBuildingKey: "townhall", serverLevel: UngatedServerLevel, levelsPerTier: LevelsPerTier);

            Assert.Equal(40, farm.AccruedAmount);
        }

        /// <summary>Сканер завершує лише ті будівництва, чий час настав.</summary>
        [Fact]
        public void CompleteDueConstructions_ShouldRaiseLevelOnlyForDueBuildings()
        {
            var village = TestData.CreateVillageWithTownhall();
            var configs = TestData.FarmConfigs();
            var farm = village.Buildings.Single(b => b.Type == "farm");
            var startedAt = DateTime.UtcNow;

            village.BeginBuildingUpgrade(farm.Id, configs, startedAt, ProductionBoost.None,
                mainBuildingKey: "townhall", serverLevel: UngatedServerLevel, levelsPerTier: LevelsPerTier);

            Assert.Equal(0, village.CompleteDueConstructions(startedAt.AddMinutes(1), configs));
            Assert.Equal(1, farm.Level.Value);

            Assert.Equal(1, village.CompleteDueConstructions(startedAt.AddMinutes(10), configs));
            Assert.Equal(2, farm.Level.Value);
            Assert.False(farm.IsUnderConstruction);
        }

        /// <summary>
        /// Правило A: рівень сервера — глобальна стеля.
        /// Контент відкривається для всіх одночасно, а не для тих, хто швидше клікає.
        /// </summary>
        [Fact]
        public void BeginBuildingUpgrade_ShouldReject_WhenServerLevelCapsTheTier()
        {
            var village = TestData.CreateVillageWithTownhall(townhallLevel: 10);
            var configs = TestData.FarmConfigs();
            var townhall = village.Buildings.Single(b => b.Type == "townhall");

            // Сервер 1 рівня дозволяє до 10; ратуша вже там
            Assert.Throws<RequirementNotMetException>(() =>
                village.BeginBuildingUpgrade(townhall.Id, configs, DateTime.UtcNow, ProductionBoost.None,
                    mainBuildingKey: "townhall", serverLevel: 1, levelsPerTier: LevelsPerTier));
        }

        /// <summary>
        /// Правило B: ратуша не переходить межу тіру, поки решта селища відстає.
        /// Це і є сенс тірів — змусити підтягувати все, а не бігти вузьким шляхом.
        /// </summary>
        [Fact]
        public void BeginBuildingUpgrade_ShouldReject_WhenTownhallCrossesTierWithLaggingBuildings()
        {
            var village = TestData.CreateVillageWithTownhall(townhallLevel: 10);
            var configs = TestData.FarmConfigs();
            var townhall = village.Buildings.Single(b => b.Type == "townhall");

            // Ферма лишилась на 1 рівні, ратуша стоїть рівно на межі тіру
            Assert.Throws<RequirementNotMetException>(() =>
                village.BeginBuildingUpgrade(townhall.Id, configs, DateTime.UtcNow, ProductionBoost.None,
                    mainBuildingKey: "townhall", serverLevel: UngatedServerLevel, levelsPerTier: LevelsPerTier));
        }

        /// <summary>Правило C: жодна будівля не переростає ратушу.</summary>
        [Fact]
        public void BeginBuildingUpgrade_ShouldReject_WhenBuildingWouldExceedTownhall()
        {
            var village = TestData.CreateVillageWithTownhall(townhallLevel: 1);
            var configs = TestData.FarmConfigs();
            var farm = village.Buildings.Single(b => b.Type == "farm");

            // Ферма 1 → 2 при ратуші 1
            Assert.Throws<RequirementNotMetException>(() =>
                village.BeginBuildingUpgrade(farm.Id, configs, DateTime.UtcNow, ProductionBoost.None,
                    mainBuildingKey: "townhall", serverLevel: UngatedServerLevel, levelsPerTier: LevelsPerTier));
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

            Assert.Throws<NotEnoughResourcesException>(() => village.ChargeCost(cost, DateTime.UtcNow));
            Assert.Equal(100, village.Resources.Single(r => r.ResourceType == "gold").Amount);
        }

        /// <summary>Фіксація буфера перед зміною буста не втрачає вироблене.</summary>
        [Fact]
        public void MaterializeProduction_ShouldBankAccruedAmountForAllBuildings()
        {
            var village = TestData.CreateVillageWithResources(1000);
            var configs = TestData.FarmConfigs();

            village.AddBuilding("farm", configs, DateTime.UtcNow);
            var building = village.Buildings.Single();
            var start = building.LastAccruedAt;

            // 4 хв під бустом ×1.5 = 60
            var boost = new ProductionBoost(1.5, start, start.AddHours(1));
            village.MaterializeProduction(configs, start.AddMinutes(4), boost);

            Assert.Equal(60, building.AccruedAmount);
        }
    }
}
