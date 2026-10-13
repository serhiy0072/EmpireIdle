using EmpireIdle.Domain.Entities;
using EmpireIdle.Domain.Services;
using EmpireIdle.Domain.Services.Config;
using EmpireIdle.Domain.ValueObjects;
using EmpireIdle.TestKit;

namespace EmpireIdle.Domain.Tests.Entities
{
    /// <summary>Звіринець їсть, а без їжі село голодує (рішення 13.10.2026).</summary>
    public class VillageFoodUpkeepTests
    {
        private const string Sawmill = "sawmill";
        private const double Hunger = 0.5;
        private static readonly DateTime Now = TestKit.Entities.Now;

        private static readonly FoodUpkeepConfig Upkeep = new()
        {
            PerPenLevelPerHour = 100,
            PerBeastPerHour = 100,
            PerBeastLevelPerHour = 10,
            HungerDefenceMultiplier = 0.5,
            HungerProductionMultiplier = Hunger
        };

        /// <summary>Село з фермою й лісопилкою; на складі по 1000 кожного ресурсу.</summary>
        private static (Village Village, Dictionary<string, BuildingConfig> Configs) Arrange()
        {
            var village = TestKit.Entities.VillageWithTownhall(townhallLevel: 1, resourceAmount: 1000);
            var configs = TestKit.Entities.FarmConfigs();
            configs[Sawmill] = new BuildingConfig
            {
                Key = Sawmill,
                ProducesResource = TestKeys.Wood,
                BaseProductionPerMinute = 10,
                BaseStorage = 1000,
                BaseBuildMinutes = 5,
                BuildTimeGrowth = 1.5,
                UpgradeCostGrowth = 1.45
            };
            village.AddBuilding(Sawmill, configs, Now);

            return (village, configs);
        }

        private static int Pay(Village village, Dictionary<string, BuildingConfig> configs, int amount, DateTime at)
            => village.PayFoodUpkeep(amount, TestKeys.Food, Hunger, configs, at, ProductionBoost.None, 1.0);

        private static long Food(Village village) => village.Resources.Single(r => r.ResourceType == TestKeys.Food).Amount;

        /// <summary>Більший звіринець, більше звірів і вищі рівні — більше їжі.</summary>
        [Fact]
        public void HourlyUpkeep_ShouldGrowWithThePenTheBeastsAndTheirLevels()
        {
            // 10 × 100 + (100 + 5 × 10) + (100 + 20 × 10)
            Assert.Equal(1450, Upkeep.HourlyUpkeep(10, [5, 20]));
            Assert.Equal(1000, Upkeep.HourlyUpkeep(10, []));
        }

        /// <summary>Немає звіринця — нічого не їсть, навіть якщо звірі десь записані.</summary>
        [Fact]
        public void HourlyUpkeep_ShouldBeZero_WithoutAPen()
            => Assert.Equal(0, Upkeep.HourlyUpkeep(0, [10]));

        /// <summary>Їжі на складі досить — вона списується, і село сите.</summary>
        [Fact]
        public void PayFoodUpkeep_ShouldTakeFoodFromTheStore()
        {
            var (village, configs) = Arrange();

            var missing = Pay(village, configs, 400, Now);

            Assert.Equal(0, missing);
            Assert.Equal(600, Food(village));
            Assert.False(village.IsHungry);
            Assert.Equal(Now, village.FoodUpkeepAt);
        }

        /// <summary>Склад порожній, але ферма наробила — звіринець доїдає з її незібраного виробітку.</summary>
        [Fact]
        public void PayFoodUpkeep_ShouldFallBackToTheFoodBuffers()
        {
            var (village, configs) = Arrange();
            var farm = village.Buildings.Single(b => b.Type == TestKeys.Farm);
            var later = Now.AddMinutes(5); // ферма 1 рівня: 10 за хвилину — 50 у буфері

            var missing = Pay(village, configs, 1030, later);

            Assert.Equal(0, missing);
            Assert.Equal(0, Food(village));
            Assert.Equal(20, farm.StoredAt(configs[TestKeys.Farm], later, ProductionBoost.None, 1.0));
            Assert.False(village.IsHungry);
        }

        /// <summary>Їжі не вистачило — голод: лісопилка далі працює вполовину, а вироблене до голоду ціле.</summary>
        [Fact]
        public void PayFoodUpkeep_ShouldStarveTheVillage_AndSlowOtherProduction()
        {
            var (village, configs) = Arrange();
            var sawmill = village.Buildings.Single(b => b.Type == Sawmill);
            var hungerAt = Now.AddMinutes(10);

            var missing = Pay(village, configs, 5000, hungerAt);

            Assert.True(missing > 0);
            Assert.True(village.IsHungry);
            // 10 хвилин до голоду по 10 і 10 хвилин голоду по 5
            Assert.Equal(150, sawmill.StoredAt(configs[Sawmill], hungerAt.AddMinutes(10), ProductionBoost.None, 1.0));
        }

        /// <summary>Їжа з'явилась — на наступному списанні голод минає, і видобуток повертається.</summary>
        [Fact]
        public void PayFoodUpkeep_ShouldEndTheHunger_OnceFoodIsEnough()
        {
            var (village, configs) = Arrange();
            var sawmill = village.Buildings.Single(b => b.Type == Sawmill);
            Pay(village, configs, 5000, Now);
            village.GrantResource(TestKeys.Food, 500, Now);

            Pay(village, configs, 100, Now);

            Assert.False(village.IsHungry);
            Assert.Equal(1.0, sawmill.HungerProductionMultiplier);
        }

        /// <summary>Голодне село захищається вдвічі гірше.</summary>
        [Fact]
        public void DefenceMultiplier_ShouldHalve_ForAHungryVillage()
        {
            var (village, configs) = Arrange();
            var config = new GameConfigBuilder().WithBuildings().Build();
            config.Beasts.FoodUpkeep = Upkeep;
            var status = new VillageStatus(new GameCatalog(config));
            var fed = status.DefenceMultiplier(village, Now);

            Pay(village, configs, 5000, Now);

            Assert.Equal(fed * 0.5, status.DefenceMultiplier(village, Now));
        }
    }
}
