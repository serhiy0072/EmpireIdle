using EmpireIdle.Domain.Entities;
using EmpireIdle.Domain.Exceptions;
using EmpireIdle.Domain.Services;
using EmpireIdle.Domain.Services.Config;
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
            var village = TestKit.Entities.VillageWithTownhall(townhallLevel: 1, resourceAmount: 1000);
            var configs = TestKit.Entities.FarmConfigs();
            var building = village.Buildings.Single(b => b.Type == TestKit.TestKeys.Farm);

            var foodBefore = village.Resources.Single(r => r.ResourceType == TestKit.TestKeys.Food).Amount;
            var collectAt = building.LastAccruedAt.AddMinutes(5);

            village.CollectFromBuilding(building.Id, configs, storageCap: 100_000, collectAt, ProductionBoost.None, 1.0);

            Assert.Equal(0, building.AccruedAmount);
            Assert.Equal(foodBefore + 50, village.Resources.Single(r => r.ResourceType == TestKit.TestKeys.Food).Amount);
        }

        /// <summary>Збір із порожнього буфера — не подія: ресурси не змінюються.</summary>
        [Fact]
        public void CollectFromBuilding_ShouldDoNothing_WhenBufferIsEmpty()
        {
            var village = TestKit.Entities.VillageWithTownhall(townhallLevel: 1, resourceAmount: 1000);
            var configs = TestKit.Entities.FarmConfigs();
            var building = village.Buildings.Single(b => b.Type == TestKit.TestKeys.Farm);

            var foodBefore = village.Resources.Single(r => r.ResourceType == TestKit.TestKeys.Food).Amount;

            village.CollectFromBuilding(building.Id, configs, storageCap: 100_000, building.LastAccruedAt, ProductionBoost.None, 1.0);

            Assert.Equal(foodBefore, village.Resources.Single(r => r.ResourceType == TestKit.TestKeys.Food).Amount);
        }

        /// <summary>
        /// Повний склад — відмова з ключем ресурсу: назву в потрібному відмінку
        /// підставляє клієнт, а буфер будівлі лишається недоторканим.
        /// </summary>
        [Fact]
        public void CollectFromBuilding_ShouldRefuse_WhenStorageIsFull()
        {
            var village = TestKit.Entities.VillageWithTownhall(townhallLevel: 1, resourceAmount: 1000);
            var configs = TestKit.Entities.FarmConfigs();
            var building = village.Buildings.Single(b => b.Type == TestKit.TestKeys.Farm);

            var refusal = Assert.Throws<RequirementNotMetException>(() => village.CollectFromBuilding(
                building.Id, configs, storageCap: 1000, building.LastAccruedAt.AddMinutes(5), ProductionBoost.None, 1.0));

            Assert.Equal(RefusalReasons.VillageStorageFull.Key, refusal.Reason);
            Assert.Equal(TestKit.TestKeys.Food, refusal.Args["resource"]);
        }

        /// <summary>Склад із місцем на частину буфера бере частину — решта чекає в будівлі.</summary>
        [Fact]
        public void CollectFromBuilding_ShouldTakeOnlyWhatFits_AndKeepTheRest()
        {
            var village = TestKit.Entities.VillageWithTownhall(townhallLevel: 1, resourceAmount: 1000);
            var configs = TestKit.Entities.FarmConfigs();
            var building = village.Buildings.Single(b => b.Type == TestKit.TestKeys.Farm);

            var collected = village.CollectFromBuilding(
                building.Id, configs, storageCap: 1020, building.LastAccruedAt.AddMinutes(5), ProductionBoost.None, 1.0);

            Assert.Equal(20, collected);
            Assert.Equal(1020, village.Resources.Single(r => r.ResourceType == TestKit.TestKeys.Food).Amount);
            Assert.Equal(30, building.AccruedAmount);
        }

        /// <summary>Під туманом будівля не виробляє, і збирати з неї нічого.</summary>
        [Fact]
        public void CollectFromBuilding_ShouldRefuse_UnderTheFog()
        {
            var configs = ConfigsWithFoggedMine();
            var village = TestKit.Entities.VillageWithTownhall(townhallLevel: 1, resourceAmount: 0);
            var mineId = village.AddBuilding(Mine, configs, TestKit.Entities.Now);

            Assert.False(village.IsProducing(village.Buildings.Single(b => b.Id == mineId), configs));
            Assert.Throws<RequirementNotMetException>(() => village.CollectFromBuilding(
                mineId, configs, storageCap: 100_000, TestKit.Entities.Now.AddMinutes(5), ProductionBoost.None, 1.0));
        }

        /// <summary>«Зібрати все» не чіпає будівлі під туманом.</summary>
        [Fact]
        public void CollectAll_ShouldSkipBuildingsUnderTheFog()
        {
            var configs = ConfigsWithFoggedMine();
            var village = TestKit.Entities.VillageWithTownhall(townhallLevel: 1, resourceAmount: 0);
            village.AddBuilding(Mine, configs, TestKit.Entities.Now);

            var summary = village.CollectAll(configs, _ => 100_000,
                TestKit.Entities.Now.AddMinutes(5), ProductionBoost.None, 1.0);

            Assert.Equal(50, summary.Collected[TestKit.TestKeys.Food]);
            Assert.False(summary.Collected.ContainsKey(TestKit.TestKeys.Iron));
            Assert.Equal(0, village.Resources.Single(r => r.ResourceType == TestKit.TestKeys.Iron).Amount);
        }

        /// <summary>
        /// Повний склад одного ресурсу не зупиняє збір решти — він лише
        /// потрапляє в список, щоб клієнт пояснив, чому буфер лишився.
        /// </summary>
        [Fact]
        public void CollectAll_ShouldCollectTheRest_WhenOneStorageIsFull()
        {
            var configs = ConfigsWithFoggedMine();
            var village = TestKit.Entities.VillageWithTownhall(townhallLevel: 2, resourceAmount: 1000);
            var farm = village.Buildings.Single(b => b.Type == TestKit.TestKeys.Farm);
            village.AddBuilding(Mine, configs, TestKit.Entities.Now);

            var summary = village.CollectAll(configs,
                resource => resource == TestKit.TestKeys.Food ? 1000 : 100_000,
                TestKit.Entities.Now.AddMinutes(5), ProductionBoost.None, 1.0);

            Assert.Equal([TestKit.TestKeys.Food], summary.FullStorages);
            Assert.Equal(50, summary.Collected[TestKit.TestKeys.Iron]);
            Assert.Equal(1050, village.Resources.Single(r => r.ResourceType == TestKit.TestKeys.Iron).Amount);
            Assert.Equal(50, farm.StoredAt(configs[TestKit.TestKeys.Farm], TestKit.Entities.Now.AddMinutes(5), ProductionBoost.None, 1.0));
        }

        /// <summary>Порожній буфер при повному складі — не привід скаржитись на склад.</summary>
        [Fact]
        public void CollectAll_ShouldNotReportAFullStorage_WhenTheBufferIsEmpty()
        {
            var village = TestKit.Entities.VillageWithTownhall(townhallLevel: 1, resourceAmount: 1000);

            var summary = village.CollectAll(TestKit.Entities.FarmConfigs(), _ => 1000,
                TestKit.Entities.Now, ProductionBoost.None, 1.0);

            Assert.Empty(summary.FullStorages);
            Assert.Empty(summary.Collected);
        }

        /// <summary>
        /// Відкриття з-під туману стартує виробіток з моменту завершення ратуші:
        /// ні запізнення сканера, ні час до відкриття не нараховуються.
        /// </summary>
        [Fact]
        public void CompleteDueConstructions_ShouldStartTheUnlockedBuildings_FromTheTownHallCompletion()
        {
            var configs = ConfigsWithFoggedMine();
            var village = TestKit.Entities.VillageWithTownhall(townhallLevel: 1, resourceAmount: 0);
            var mineId = village.AddBuilding(Mine, configs, TestKit.Entities.Now);
            var mine = village.Buildings.Single(b => b.Id == mineId);
            var townhall = village.Buildings.Single(b => b.Type == TestKit.TestKeys.Townhall);

            townhall.BeginUpgrade(configs[TestKit.TestKeys.Townhall], TimeSpan.FromMinutes(10),
                TestKit.Entities.Now, ProductionBoost.None, 1.0);

            // Сканер прийшов на 20 хвилин пізніше за завершення
            village.CompleteDueConstructions(TestKit.Entities.Now.AddMinutes(30), configs);

            Assert.True(village.IsProducing(mine, configs));
            // 2 хвилини після відкриття × 10/хв; без скидання буфер стояв би на капі 60
            Assert.Equal(20, mine.StoredAt(configs[Mine], TestKit.Entities.Now.AddMinutes(12), ProductionBoost.None, 1.0));
        }

        /// <summary>Фіксація перед зміною множника не банкує вироблене «під туманом».</summary>
        [Fact]
        public void MaterializeProduction_ShouldSkipBuildingsUnderTheFog()
        {
            var configs = ConfigsWithFoggedMine();
            var village = TestKit.Entities.VillageWithTownhall(townhallLevel: 1, resourceAmount: 0);
            var mineId = village.AddBuilding(Mine, configs, TestKit.Entities.Now);

            village.MaterializeProduction(configs, TestKit.Entities.Now.AddMinutes(5), ProductionBoost.None, 1.0);

            Assert.Equal(0, village.Buildings.Single(b => b.Id == mineId).AccruedAmount);
        }

        private const string Mine = "mine";

        /// <summary>Ферма з ратушею плюс шахта заліза, що відкривається з ратушею 2.</summary>
        private static Dictionary<string, BuildingConfig> ConfigsWithFoggedMine()
        {
            var configs = TestKit.Entities.FarmConfigs();

            configs[Mine] = new BuildingConfig
            {
                Key = Mine,
                ProducesResource = TestKit.TestKeys.Iron,
                BaseProductionPerMinute = 10,
                BaseStorage = 60,
                BaseBuildMinutes = 5,
                BuildTimeGrowth = 1.5,
                UpgradeCostGrowth = 1.45,
                RequiresMainBuildingLevel = 2
            };

            return configs;
        }

        [Fact]
        public void RelocateTo_ShouldRefuse_TheCellItAlreadyStandsOn()
        {
            var village = TestKit.Entities.Village(x: 4, y: 7);

            var refusal = Assert.Throws<RequirementNotMetException>(() => village.RelocateTo(4, 7, TestKit.Entities.Now));

            Assert.Equal(RefusalReasons.VillageAlreadyThere.Key, refusal.Reason);
        }

        /// <summary>Додавання будівлі кладе її в колекцію з правильним VillageId.</summary>
        [Fact]
        public void AddBuilding_ShouldPlaceBuildingInVillage()
        {
            var village = TestKit.Entities.VillageWithResources(200);
            var configs = TestKit.Entities.FarmConfigs();

            village.AddBuilding(TestKit.TestKeys.Farm, configs, TestKit.Entities.Now);

            Assert.Single(village.Buildings);
            Assert.Equal(village.Id, village.Buildings.First().VillageId);
        }

        /// <summary>Кожна будівля унікальна — другу такого ж типу поставити не можна.</summary>
        [Fact]
        public void AddBuilding_ShouldRejectDuplicateType()
        {
            var village = TestKit.Entities.VillageWithResources(1000);
            var configs = TestKit.Entities.FarmConfigs();

            village.AddBuilding(TestKit.TestKeys.Farm, configs, TestKit.Entities.Now);

            var refusal = Assert.Throws<AlreadyExistsException>(() => village.AddBuilding(TestKit.TestKeys.Farm, configs, TestKit.Entities.Now));
            Assert.Null(refusal.Reason);
        }

        /// <summary>
        /// Апгрейд списує вартість за геометричною кривою й ставить будівлю
        /// в стан будівництва. Ферма 1 рівня: 100 × 1.45^0 = 100.
        /// </summary>
        [Fact]
        public void BeginBuildingUpgrade_ShouldChargeCostAndStartConstruction()
        {
            var village = TestKit.Entities.VillageWithTownhall(townhallLevel: 10, resourceAmount: 300);
            var configs = TestKit.Entities.FarmConfigs();
            var farm = village.Buildings.Single(b => b.Type == TestKit.TestKeys.Farm);
            var now = TestKit.Entities.Now;

            var foodBefore = village.Resources.Single(r => r.ResourceType == TestKit.TestKeys.Food).Amount;

            village.BeginBuildingUpgrade(farm.Id, configs, now, ProductionBoost.None,
                mainBuildingKey: TestKit.TestKeys.Townhall, serverLevel: UngatedServerLevel, levelsPerTier: LevelsPerTier, locationMultiplier: 1.0);

            Assert.True(farm.IsUnderConstruction);
            Assert.NotNull(farm.ConstructionCompletesAt);
            Assert.Equal(foodBefore - 100, village.Resources.Single(r => r.ResourceType == TestKit.TestKeys.Food).Amount);
        }

        /// <summary>Апгрейд банкує вироблене до зупинки — воно не губиться.</summary>
        [Fact]
        public void BeginBuildingUpgrade_ShouldBankProductionBeforeFreezing()
        {
            var village = TestKit.Entities.VillageWithTownhall(townhallLevel: 10, resourceAmount: 1000);
            var configs = TestKit.Entities.FarmConfigs();
            var farm = village.Buildings.Single(b => b.Type == TestKit.TestKeys.Farm);

            village.BeginBuildingUpgrade(farm.Id, configs, farm.LastAccruedAt.AddMinutes(4), ProductionBoost.None,
                mainBuildingKey: TestKit.TestKeys.Townhall, serverLevel: UngatedServerLevel, levelsPerTier: LevelsPerTier, locationMultiplier: 1.0);

            Assert.Equal(40, farm.AccruedAmount);
        }

        /// <summary>Сканер завершує лише ті будівництва, чий час настав.</summary>
        [Fact]
        public void CompleteDueConstructions_ShouldRaiseLevelOnlyForDueBuildings()
        {
            var village = TestKit.Entities.VillageWithTownhall(townhallLevel: 10, resourceAmount: 1000);
            var configs = TestKit.Entities.FarmConfigs();
            var farm = village.Buildings.Single(b => b.Type == TestKit.TestKeys.Farm);
            var startedAt = TestKit.Entities.Now;

            village.BeginBuildingUpgrade(farm.Id, configs, startedAt, ProductionBoost.None,
                mainBuildingKey: TestKit.TestKeys.Townhall, serverLevel: UngatedServerLevel, levelsPerTier: LevelsPerTier, locationMultiplier: 1.0);

            Assert.Equal(0, village.CompleteDueConstructions(startedAt.AddMinutes(1), configs));
            Assert.Equal(1, farm.Level.Value);

            // Рівень 1 несе "ранній податок" (×3): 5 базових хвилин стають 15
            Assert.Equal(1, village.CompleteDueConstructions(startedAt.AddMinutes(16), configs));
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
            var village = TestKit.Entities.VillageWithTownhall(townhallLevel: 10, resourceAmount: 1000);
            var configs = TestKit.Entities.FarmConfigs();
            var townhall = village.Buildings.Single(b => b.Type == TestKit.TestKeys.Townhall);

            // Сервер 1 рівня дозволяє до 10; ратуша вже там
            var refusal = Assert.Throws<RequirementNotMetException>(() =>
                village.BeginBuildingUpgrade(townhall.Id, configs, TestKit.Entities.Now, ProductionBoost.None,
                    mainBuildingKey: TestKit.TestKeys.Townhall, serverLevel: 1, levelsPerTier: LevelsPerTier, locationMultiplier: 1.0));
            Assert.Equal(RefusalReasons.BuildingServerCeiling.Key, refusal.Reason);
        }

        /// <summary>
        /// Правило B: ратуша не переходить межу тіру, поки решта селища відстає.
        /// Це і є сенс тірів — змусити підтягувати все, а не бігти вузьким шляхом.
        /// </summary>
        [Fact]
        public void BeginBuildingUpgrade_ShouldReject_WhenTownhallCrossesTierWithLaggingBuildings()
        {
            // Ратуша вже на межі (рівень 10), а ферма відстає (рівень 1).
            // Спроба підняти ратушу до рівня 11 (перехід тіру) має бути заблокована.
            var village = TestKit.Entities.VillageWithTownhall(townhallLevel: 10, resourceAmount: 1000);
            var configs = TestKit.Entities.FarmConfigs();
            var townhall = village.Buildings.Single(b => b.Type == TestKit.TestKeys.Townhall);

            // Опускаємо ферму до 1 рівня спеціально для перевірки відставання
            var farm = village.Buildings.Single(b => b.Type == TestKit.TestKeys.Farm);
            // (або якщо рівня за замовчуванням достатньо — головне, щоб ратуша була рівно на межі 10)

            var refusal = Assert.Throws<RequirementNotMetException>(() =>
                village.BeginBuildingUpgrade(townhall.Id, configs, TestKit.Entities.Now, ProductionBoost.None,
                    mainBuildingKey: TestKit.TestKeys.Townhall, serverLevel: UngatedServerLevel, levelsPerTier: LevelsPerTier, locationMultiplier: 1.0));
            Assert.Equal(RefusalReasons.BuildingVillageLagging.Key, refusal.Reason);
        }

        /// <summary>Правило C: жодна будівля не переростає ратушу.</summary>
        [Fact]
        public void BeginBuildingUpgrade_ShouldReject_WhenBuildingWouldExceedTownhall()
        {
            var village = TestKit.Entities.VillageWithTownhall(townhallLevel: 1, resourceAmount: 1000);
            var configs = TestKit.Entities.FarmConfigs();
            var farm = village.Buildings.Single(b => b.Type == TestKit.TestKeys.Farm);

            // Ферма 1 → 2 при ратуші 1 (ратуша 1, тому ферма не може стати 2)
            var refusal = Assert.Throws<RequirementNotMetException>(() =>
                village.BeginBuildingUpgrade(farm.Id, configs, TestKit.Entities.Now, ProductionBoost.None,
                    mainBuildingKey: TestKit.TestKeys.Townhall, serverLevel: UngatedServerLevel, levelsPerTier: LevelsPerTier, locationMultiplier: 1.0));
            Assert.Equal(RefusalReasons.BuildingTownHallCeiling.Key, refusal.Reason);
        }

        /// <summary>
        /// ChargeCost списує все або нічого: при нестачі одного ресурсу
        /// решта лишається недоторканою.
        /// </summary>
        [Fact]
        public void ChargeCost_ShouldNotChargeAnything_WhenOneResourceIsInsufficient()
        {
            var village = TestKit.Entities.Village();
            village.Resources.Single(r => r.ResourceType == TestKit.TestKeys.Gold).Add(100);
            village.Resources.Single(r => r.ResourceType == TestKit.TestKeys.Food).Add(10);
            var cost = new List<ResourceCost>
            {
                new() { Resource = TestKit.TestKeys.Gold, Amount = 10 },
                new() { Resource = TestKit.TestKeys.Food, Amount = 50 } // не вистачає
            };

            Assert.Throws<NotEnoughResourcesException>(() => village.ChargeCost(cost, TestKit.Entities.Now));
            Assert.Equal(100, village.Resources.Single(r => r.ResourceType == TestKit.TestKeys.Gold).Amount);
        }

        /// <summary>Фіксація буфера перед зміною буста не втрачає вироблене.</summary>
        [Fact]
        public void MaterializeProduction_ShouldBankAccruedAmountForAllBuildings()
        {
            var village = TestKit.Entities.VillageWithTownhall(townhallLevel: 10, resourceAmount: 1000);
            var configs = TestKit.Entities.FarmConfigs();
            var building = village.Buildings.Single(b => b.Type == TestKit.TestKeys.Farm);
            var start = building.LastAccruedAt;

            // 4 хв під бустом ×1.5 = 60
            var boost = new ProductionBoost(1.5, start, start.AddHours(1));
            village.MaterializeProduction(configs, start.AddMinutes(4), boost, 1.0);

            Assert.Equal(60, building.AccruedAmount);
        }

        /// <summary>
        /// Ферма з буфером плюс склад із захищеним запасом.
        /// Захищено 40 їжі за рівень, вміщає 500.
        /// </summary>
        private static Dictionary<string, BuildingConfig> PlunderConfigs()
        {
            var configs = TestKit.Entities.FarmConfigs();

            configs[TestKit.TestKeys.Warehouse] = new BuildingConfig
            {
                Key = TestKit.TestKeys.Warehouse,
                StoresResources = [TestKit.TestKeys.Food],
                BaseStorage = 500,
                ProtectedStorage = 40,
                Cost = [new ResourceCost { Resource = TestKit.TestKeys.Wood, Amount = 100 }],
                BaseBuildMinutes = 5,
                BuildTimeGrowth = 1.5,
                UpgradeCostGrowth = 1.45,
                RequiresMainBuildingLevel = 0
            };

            return configs;
        }

        /// <summary>
        /// Калькулятор грабунку на тому самому конфігу, що й PlunderConfigs.
        /// Каталог мінімальний: грабунку потрібні лише будівлі.
        /// </summary>
        private static PlunderCalculator Plunderer(Dictionary<string, BuildingConfig> configs)
        {
            var catalog = new GameCatalog(new GameConfig
            {
                Buildings = configs.Values.ToList()
            });

            return new PlunderCalculator(catalog, new VillageCapacities(catalog));
        }

        /// <summary>
        /// Село, де є лише їжа: решта ресурсів нульові навмисно.
        /// Ресурс без сховища не має захищеного запасу й грабується
        /// повністю — це правильно, але заважає міряти саме запас.
        /// </summary>
        private static Village VillageWithStoredFood(int food, Dictionary<string, BuildingConfig> configs)
        {
            var village = TestKit.Entities.VillageWithResources(0);

            village.GrantStartingResources(new Dictionary<string, int> { [TestKit.TestKeys.Food] = food }, TestKit.Entities.Now);
            village.AddBuilding(TestKit.TestKeys.Warehouse, configs, TestKit.Entities.Now);

            return village;
        }

        /// <summary>
        /// Понад захищений запас нападник забирає, сам запас — ні.
        /// Це те, що робить набіг «неприємним, але не катастрофою».
        /// </summary>
        [Fact]
        public void Plunder_ShouldLeaveTheProtectedReserve()
        {
            // Arrange: 100 їжі на складі, захищено 40, армія винесе скільки завгодно
            var configs = PlunderConfigs();
            var village = VillageWithStoredFood(100, configs);

            var plunder = Plunderer(configs);

            // Act
            var loot = plunder.Plunder(village, carryCapacity: 1000, ProductionBoost.None, 1.0, TestKit.Entities.Now);

            // Assert
            Assert.Equal(60, loot[TestKit.TestKeys.Food]);
            Assert.Equal(40, village.Resources.Single(r => r.ResourceType == TestKit.TestKeys.Food).Amount);
        }

        /// <summary>Нижче захищеного запасу брати нема чого — набіг порожній.</summary>
        [Fact]
        public void Plunder_ShouldTakeNothing_WhenStoreIsBelowTheReserve()
        {
            // Arrange
            var configs = PlunderConfigs();
            var village = VillageWithStoredFood(30, configs);

            var plunder = Plunderer(configs);

            // Act
            var loot = plunder.Plunder(village, carryCapacity: 1000, ProductionBoost.None, 1.0, TestKit.Entities.Now);

            // Assert
            Assert.Empty(loot);
            Assert.Equal(30, village.Resources.Single(r => r.ResourceType == TestKit.TestKeys.Food).Amount);
        }

        /// <summary>
        /// Буфер будівлі захисту не має: невибраний виробіток іде повністю,
        /// і саме це карає того, хто давно не заходив.
        /// </summary>
        [Fact]
        public void Plunder_ShouldEmptyBuildingBuffersFirst()
        {
            // Arrange: ферма виробила 50 за 5 хвилин, на складі рівно запас
            var configs = PlunderConfigs();
            var village = VillageWithStoredFood(40, configs);

            village.AddBuilding(TestKit.TestKeys.Farm, configs, TestKit.Entities.Now);

            var farm = village.Buildings.Single(b => b.Type == TestKit.TestKeys.Farm);
            var plunderAt = farm.LastAccruedAt.AddMinutes(5);

            var plunder = Plunderer(configs);

            // Act
            var loot = plunder.Plunder(village, carryCapacity: 50, ProductionBoost.None, 1.0, plunderAt);

            // Assert
            Assert.Equal(50, loot[TestKit.TestKeys.Food]);
            Assert.Equal(0, farm.AccruedAmount);

            // Склад лишився недоторканим — там рівно захищений запас
            Assert.Equal(40, village.Resources.Single(r => r.ResourceType == TestKit.TestKeys.Food).Amount);
        }

        /// <summary>Вантажопідйомність — стеля: решта лишається в селі.</summary>
        [Fact]
        public void Plunder_ShouldStopAtCarryCapacity()
        {
            // Arrange: доступно 60 понад запас, армія винесе лише 25
            var configs = PlunderConfigs();
            var village = VillageWithStoredFood(100, configs);

            var plunder = Plunderer(configs);

            // Act
            var loot = plunder.Plunder(village, carryCapacity: 25, ProductionBoost.None, 1.0, TestKit.Entities.Now);

            // Assert
            Assert.Equal(25, loot[TestKit.TestKeys.Food]);
            Assert.Equal(75, village.Resources.Single(r => r.ResourceType == TestKit.TestKeys.Food).Amount);
        }

        /// <summary>
        /// Ресурс без сховища грабується повністю: захищений запас
        /// дає будівля, а не сам факт наявності ресурсу.
        /// </summary>
        [Fact]
        public void Plunder_ShouldTakeEverything_WhenResourceHasNoStorage()
        {
            // Arrange: склад описаний лише для food, дерево лежить без захисту
            var configs = PlunderConfigs();
            var village = TestKit.Entities.VillageWithResources(0);

            village.GrantStartingResources(new Dictionary<string, int> { [TestKit.TestKeys.Wood] = 70 }, TestKit.Entities.Now);
            village.AddBuilding(TestKit.TestKeys.Warehouse, configs, TestKit.Entities.Now);

            var plunder = Plunderer(configs);

            // Act
            var loot = plunder.Plunder(village, carryCapacity: 1000, ProductionBoost.None, 1.0, TestKit.Entities.Now);

            // Assert
            Assert.Equal(70, loot[TestKit.TestKeys.Wood]);
            Assert.Equal(0, village.Resources.Single(r => r.ResourceType == TestKit.TestKeys.Wood).Amount);
        }

        /// <summary>Будівля під туманом нічого не виробила — нападнику з неї нічого.</summary>
        [Fact]
        public void Plunder_ShouldSkipBuildingsUnderTheFog()
        {
            // Каталог вимагає склад для кожного вироблюваного ресурсу
            var configs = ConfigsWithFoggedMine();
            configs[TestKit.TestKeys.Warehouse] = new BuildingConfig
            {
                Key = TestKit.TestKeys.Warehouse,
                StoresResources = [TestKit.TestKeys.Food, TestKit.TestKeys.Iron],
                BaseStorage = 500,
                Cost = [new ResourceCost { Resource = TestKit.TestKeys.Wood, Amount = 100 }],
                BaseBuildMinutes = 5,
                BuildTimeGrowth = 1.5,
                UpgradeCostGrowth = 1.45
            };

            var village = TestKit.Entities.VillageWithTownhall(townhallLevel: 1, resourceAmount: 0);
            village.AddBuilding(Mine, configs, TestKit.Entities.Now);
            village.AddBuilding(TestKit.TestKeys.Warehouse, configs, TestKit.Entities.Now);

            var loot = Plunderer(configs).Plunder(village, carryCapacity: 1000, ProductionBoost.None, 1.0,
                TestKit.Entities.Now.AddMinutes(5));

            Assert.False(loot.ContainsKey(TestKit.TestKeys.Iron));
            Assert.Equal(50, loot[TestKit.TestKeys.Food]);
        }
    }
}
