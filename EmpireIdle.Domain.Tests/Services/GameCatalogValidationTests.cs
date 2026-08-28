using EmpireIdle.Domain.Services;

namespace EmpireIdle.Domain.Tests.Services
{
    /// <summary>
    /// Конфіг редагується руками, і компілятор його не перевіряє —
    /// ці правила єдине, що стоїть між помилкою в JSON і живим сервером.
    /// </summary>
    public class GameCatalogValidationTests
    {
        /// <summary>Мінімальний валідний конфіг: ратуша, ферма, склад під їжу.</summary>
        private static GameConfig MinimalConfig() => new()
        {
            Buildings =
            [
                new BuildingConfig { Key = "townhall", IsMainBuilding = true, UpgradeCostGrowth = 1.45 },
                new BuildingConfig
                {
                    Key = "farm",
                    ProducesResource = "food",
                    UpgradeCostGrowth = 1.45,
                    RequiresMainBuildingLevel = 0
                },
                new BuildingConfig
                {
                    Key = "warehouse",
                    StoresResources = ["food"],
                    UpgradeCostGrowth = 1.45
                }
            ]
        };

        [Fact]
        public void Validate_ShouldAcceptAMinimalConsistentConfig()
        {
            var catalog = new GameCatalog(MinimalConfig());

            Assert.Equal("townhall", catalog.MainBuildingKey);
        }

        /// <summary>Growth нижче 1.0 здешевлював би апгрейд із кожним рівнем.</summary>
        [Fact]
        public void Validate_ShouldRejectShrinkingUpgradeCost()
        {
            var config = MinimalConfig();
            config.Buildings[1].UpgradeCostGrowth = 0.9;

            var error = Assert.Throws<InvalidOperationException>(() => new GameCatalog(config));

            Assert.Contains("farm", error.Message);
        }

        /// <summary>
        /// Нуль означає «не задано» — мінімальні фікстури тестів не описують
        /// криві вартості, і валідація не має їх ламати.
        /// </summary>
        [Fact]
        public void Validate_ShouldTreatZeroGrowthAsUnset()
        {
            var config = MinimalConfig();
            config.Buildings[1].UpgradeCostGrowth = 0;

            var catalog = new GameCatalog(config);

            Assert.NotNull(catalog);
        }

        /// <summary>
        /// Будівля не може коштувати ресурс, який відкривається пізніше за неї:
        /// стартового запасу вистачить ненадовго, і вона стане недосяжною.
        /// </summary>
        [Fact]
        public void Validate_ShouldRejectCostInAResourceUnlockedLater()
        {
            var config = MinimalConfig();

            config.Buildings.Add(new BuildingConfig
            {
                Key = "ironmine",
                ProducesResource = "iron",
                RequiresMainBuildingLevel = 5,
                UpgradeCostGrowth = 1.45
            });

            config.Buildings.Add(new BuildingConfig
            {
                Key = "bank",
                StoresResources = ["iron"],
                UpgradeCostGrowth = 1.45
            });

            // Казарма на 1 рівні коштує заліза, яке з'явиться на 5
            config.Buildings.Add(new BuildingConfig
            {
                Key = "barracks",
                RequiresMainBuildingLevel = 1,
                UpgradeCostGrowth = 1.45,
                Cost = [new ResourceCost { Resource = "iron", Amount = 100 }]
            });

            var error = Assert.Throws<InvalidOperationException>(() => new GameCatalog(config));

            Assert.Contains("barracks", error.Message);
        }

        /// <summary>
        /// Ресурс без сховища накопичувався б без ліміту й тихо ламав
        /// економіку складу — помилка, яку помітили б через тиждень.
        /// </summary>
        [Fact]
        public void Validate_ShouldRejectAProducedResourceWithoutStorage()
        {
            var config = MinimalConfig();

            config.Buildings.Add(new BuildingConfig
            {
                Key = "goldmine",
                ProducesResource = "gold",
                UpgradeCostGrowth = 1.45
            });

            var error = Assert.Throws<InvalidOperationException>(() => new GameCatalog(config));

            Assert.Contains("gold", error.Message);
        }

        /// <summary>Меж кілець має бути рівно на одну менше за множники.</summary>
        [Fact]
        public void Validate_ShouldRejectMismatchedRingCounts()
        {
            var config = MinimalConfig();
            config.Map.Geometry.RingBoundaries = [0.2, 0.5];
            config.Map.Geometry.RingMultipliers = [2.0, 1.0];

            Assert.Throws<InvalidOperationException>(() => new GameCatalog(config));
        }

        /// <summary>Межі йдуть назовні: невпорядкований список зробив би кільце недосяжним.</summary>
        [Fact]
        public void Validate_ShouldRejectBoundariesThatDoNotIncrease()
        {
            var config = MinimalConfig();
            config.Map.Geometry.RingBoundaries = [0.5, 0.2];
            config.Map.Geometry.RingMultipliers = [2.0, 1.4, 1.0];

            var error = Assert.Throws<InvalidOperationException>(() => new GameCatalog(config));

            Assert.Contains("increase outward", error.Message);
        }

        /// <summary>Межа — частка радіуса, тому понад 1.0 виводить кільце за карту.</summary>
        [Fact]
        public void Validate_ShouldRejectBoundariesBeyondTheRadius()
        {
            var config = MinimalConfig();
            config.Map.Geometry.RingBoundaries = [0.2, 1.5];
            config.Map.Geometry.RingMultipliers = [2.0, 1.4, 1.0];

            Assert.Throws<InvalidOperationException>(() => new GameCatalog(config));
        }

        /// <summary>Порожня геометрія пропускається: фікстура може її не описувати.</summary>
        [Fact]
        public void Validate_ShouldSkipGeometry_WhenItIsNotConfigured()
        {
            var catalog = new GameCatalog(MinimalConfig());

            Assert.Empty(catalog.Config.Map.Geometry.RingBoundaries);
        }
    }
}
