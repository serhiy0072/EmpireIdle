using EmpireIdle.Domain.Entities;
using EmpireIdle.Domain.Exceptions;
using EmpireIdle.Domain.Services;
using EmpireIdle.Domain.Services.Config;
using EmpireIdle.Domain.ValueObjects;

namespace EmpireIdle.Domain.Tests.Entities
{
    /// <summary>
    /// Пошкодження після програної оборони (GDD §2.6): половинний темп,
    /// самовідновлення за годинник, ремонт за ресурси й серія поразок.
    /// </summary>
    public class BuildingDamageTests
    {
        private static readonly DateTime Now = TestKit.Entities.Now;
        private static readonly TimeSpan RepairIn = TimeSpan.FromHours(7);

        private static GameConfig Config() => new()
        {
            Buildings =
            [
                new BuildingConfig { Key = "townhall", IsMainBuilding = true, UpgradeCostGrowth = 1.0 },
                new BuildingConfig
                {
                    Key = "farm", ProducesResource = "food", BaseProductionPerMinute = 10, BaseStorage = 1_000_000,
                    UpgradeCostGrowth = 1.0, Cost = [new ResourceCost { Resource = "food", Amount = 1000 }]
                },
                new BuildingConfig { Key = "warehouse", StoresResources = ["food"], BaseStorage = 1_000_000, UpgradeCostGrowth = 1.0 },
                new BuildingConfig
                {
                    Key = "wall", DefenceBonusPerLevel = 0.2, UpgradeCostGrowth = 1.0,
                    Cost = [new ResourceCost { Resource = "food", Amount = 500 }]
                }
            ],
            Combat = new CombatConfig
            {
                CityFall = new CityFallConfig { Enabled = true, DefenceLossPerDamage = 0.25, RepairCostShare = 0.1 }
            }
        };

        private static GameCatalog Catalog() => new(Config());

        private static Village NewVillage(int food = 0)
        {
            var catalog = Catalog();
            var village = new Village(Guid.NewGuid(), Guid.NewGuid(), "Test", ["food"], 0, 0);

            village.GrantStartingResources(new Dictionary<string, int> { ["food"] = food }, Now);
            village.AddBuilding("townhall", catalog.Buildings, Now);
            village.AddBuilding("farm", catalog.Buildings, Now);
            village.AddBuilding("wall", catalog.Buildings, Now);

            return village;
        }

        private static Building Farm(Village village) => village.Buildings.Single(b => b.Type == "farm");

        private static Building Wall(Village village) => village.Buildings.Single(b => b.Type == "wall");

        private static int Defeat(Village village, DateTime at, params Building[] buildings)
            => village.SufferDefeat(buildings.Select(b => b.Id).ToList(), Catalog().Buildings, at, RepairIn, 0.5,
                ProductionBoost.None, 1.0);

        // ---------- Виробництво ----------

        /// <summary>Пошкоджена будівля виробляє вдвічі повільніше.</summary>
        [Fact]
        public void StoredAt_ShouldHalveTheRate_WhileDamaged()
        {
            var village = NewVillage();
            var farm = Farm(village);
            var config = Catalog().Buildings["farm"];

            Defeat(village, Now, farm);

            // 60 хвилин × 10/хв × 0.5
            Assert.Equal(300, farm.StoredAt(config, Now.AddHours(1), ProductionBoost.None, 1.0));
        }

        /// <summary>Будівля відновилась посеред інтервалу — далі темп повний.</summary>
        [Fact]
        public void StoredAt_ShouldReturnToFullRate_AfterTheSelfRepair()
        {
            var village = NewVillage();
            var farm = Farm(village);
            var config = Catalog().Buildings["farm"];

            Defeat(village, Now, farm);

            // 7 год за половинним темпом + 1 год за повним
            var expected = 7 * 60 * 10 / 2 + 60 * 10;

            Assert.Equal(expected, farm.StoredAt(config, Now.AddHours(8), ProductionBoost.None, 1.0));
        }

        /// <summary>Буст і пошкодження множаться там, де перекриваються.</summary>
        [Fact]
        public void StoredAt_ShouldCombineTheBoostAndTheDamage()
        {
            var village = NewVillage();
            var farm = Farm(village);
            var config = Catalog().Buildings["farm"];

            Defeat(village, Now, farm);

            var boost = new ProductionBoost(2.0, Now, Now.AddHours(1));

            // Перша година: ×2 × 0.5 = ×1; друга: лише пошкодження ×0.5
            Assert.Equal(600 + 300, farm.StoredAt(config, Now.AddHours(2), boost, 1.0));
        }

        // ---------- Глибина пошкодження ----------

        [Fact]
        public void TakeDamage_ShouldAccumulate_WhileTheBuildingIsStillDamaged()
        {
            var village = NewVillage();
            var wall = Wall(village);

            Defeat(village, Now, wall);
            Defeat(village, Now.AddHours(1), wall);

            Assert.Equal(2, wall.DamageAt(Now.AddHours(2)));
            Assert.Equal(Now.AddHours(1) + RepairIn, wall.DamagedUntil);
        }

        [Fact]
        public void TakeDamage_ShouldStartOver_AfterTheSelfRepair()
        {
            var village = NewVillage();
            var wall = Wall(village);

            Defeat(village, Now, wall);
            Defeat(village, Now.AddHours(8), wall);

            Assert.Equal(1, wall.DamageAt(Now.AddHours(8)));
        }

        /// <summary>Кожен рівень пошкодження забирає частку бонусу стіни.</summary>
        [Fact]
        public void DefenceMultiplier_ShouldShrink_WithTheWallDamage()
        {
            var village = NewVillage();
            var status = new VillageStatus(Catalog());

            Assert.Equal(1.2, status.DefenceMultiplier(village, Now), 6);

            Defeat(village, Now, Wall(village));
            Defeat(village, Now, Wall(village));

            // 0.2 × (1 − 2 × 0.25)
            Assert.Equal(1.1, status.DefenceMultiplier(village, Now.AddMinutes(1)), 6);
            Assert.Equal(1.2, status.DefenceMultiplier(village, Now.AddHours(8)), 6);
        }

        // ---------- Серія поразок ----------

        [Fact]
        public void SufferDefeat_ShouldExtendTheStreak_WhileSomethingIsDamaged()
        {
            var village = NewVillage();

            Assert.Equal(1, Defeat(village, Now, Farm(village)));
            Assert.Equal(2, Defeat(village, Now.AddHours(1), Wall(village)));
        }

        /// <summary>Усе відновилось саме — наступна поразка починає нову серію.</summary>
        [Fact]
        public void SufferDefeat_ShouldStartANewStreak_AfterEverythingRepairedItself()
        {
            var village = NewVillage();

            Defeat(village, Now, Farm(village));
            Defeat(village, Now.AddHours(1), Farm(village));

            Assert.Equal(0, village.DefeatStreakAt(Now.AddHours(9)));
            Assert.Equal(1, Defeat(village, Now.AddHours(9), Farm(village)));
        }

        // ---------- Ремонт за ресурси ----------

        /// <summary>Ціна — частка вартості апгрейду за кожен рівень пошкодження.</summary>
        [Fact]
        public void RepairCost_ShouldScaleWithTheDamage()
        {
            var village = NewVillage();

            Defeat(village, Now, Farm(village), Wall(village));
            Defeat(village, Now, Wall(village));

            // Ферма: 1000 × 0.1 × 1; стіна: 500 × 0.1 × 2
            var cost = Assert.Single(village.RepairCost(Catalog().Buildings, Now, 0.1));
            Assert.Equal(200, cost.Amount);
        }

        [Fact]
        public void RepairAll_ShouldChargeRepairEverythingAndEndTheStreak()
        {
            var village = NewVillage(food: 1000);

            Defeat(village, Now, Farm(village), Wall(village));
            village.RepairAll(Catalog().Buildings, Now.AddMinutes(1), 0.1, ProductionBoost.None, 1.0);

            Assert.False(village.HasDamageAt(Now.AddMinutes(1)));
            Assert.Equal(0, village.DefeatStreakAt(Now.AddMinutes(1)));
            Assert.Equal(850, village.Resources.Single(r => r.ResourceType == "food").Amount);
        }

        [Fact]
        public void RepairAll_ShouldRefuse_WhenNothingIsDamaged()
        {
            var village = NewVillage(food: 1000);

            var refusal = Assert.Throws<RequirementNotMetException>(() =>
                village.RepairAll(Catalog().Buildings, Now, 0.1, ProductionBoost.None, 1.0));

            Assert.Equal(RefusalReasons.VillageNothingToRepair.Key, refusal.Reason);
        }

        // ---------- Вибір будівель ----------

        /// <summary>Перша поразка серії б'є по стінах і двох випадкових.</summary>
        [Fact]
        public void PickDamaged_ShouldTakeTheWallsAndTwoMore_OnTheFirstDefeat()
        {
            var village = NewVillage();

            var picked = new CityFallRules(Catalog()).PickDamaged(village, firstInStreak: true, new SystemRandomSource());

            Assert.Equal(3, picked.Count);
            Assert.Contains(Wall(village).Id, picked);
        }

        /// <summary>Наступні поразки — лише дві випадкові, без повторів.</summary>
        [Fact]
        public void PickDamaged_ShouldTakeTwoDistinctBuildings_OnTheNextDefeats()
        {
            var village = NewVillage();

            var picked = new CityFallRules(Catalog()).PickDamaged(village, firstInStreak: false, new SystemRandomSource());

            Assert.Equal(2, picked.Count);
            Assert.Equal(2, picked.Distinct().Count());
        }

        [Theory]
        [InlineData(2, false)]
        [InlineData(3, true)]
        public void Evicts_ShouldFireOnTheThirdDefeatInARow(int streak, bool expected)
        {
            Assert.Equal(expected, new CityFallRules(Catalog()).Evicts(streak));
        }
    }
}
