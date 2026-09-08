using EmpireIdle.Domain.Services;
using EmpireIdle.Domain.Services.Config;

namespace EmpireIdle.Domain.Tests.Services
{
    /// <summary>
    /// Валідатор конфіга — єдине, що стоїть між битим JSON і грою.
    /// Кожен тест ламає рівно одну річ у валідному конфігу: так видно,
    /// що саме правило спрацювало, а не якесь інше.
    /// </summary>
    public class GameConfigValidatorTests
    {
        /// <summary>
        /// Мінімально валідний конфіг: ратуша, ферма, склад, ресурс,
        /// коректна геометрія й рейтинг.
        /// </summary>
        private static GameConfig ValidConfig() => new()
        {
            Buildings =
            [
                new BuildingConfig { Key = "townhall", IsMainBuilding = true, UpgradeCostGrowth = 1.45 },
                new BuildingConfig
                {
                    Key = "farm", ProducesResource = "food", RequiresMainBuildingLevel = 0,
                    UpgradeCostGrowth = 1.45,
                    Cost = [new ResourceCost { Resource = "food", Amount = 100 }]
                },
                new BuildingConfig { Key = "warehouse", StoresResources = ["food"], BaseStorage = 1000,
                    UpgradeCostGrowth = 1.45 }
            ],
            Resources = [new ResourceConfig { Key = "food" }],
            StartingResources = new Dictionary<string, int> { ["food"] = 100 },
            ActiveServerIds = [1],
            DefaultServerId = 1,
            Map = new MapConfig
            {
                Width = 100,
                Height = 100,
                TerrainSeed = 1,
                Terrains = [new TerrainConfig { Type = "plain", Weight = 1, Passable = true, MoveCost = 1.0, Habitable = true }],
                Geometry = new MapGeometryConfig
                {
                    RingBoundaries = [0.2, 0.5],
                    RingMultipliers = [2.0, 1.4, 1.0],
                    FogMinShare = 0.4,
                    FogMaxShare = 1.0
                }
            },
            Rating = new RatingConfig
            {
                PowerWeight = 0.5,
                DevelopmentWeight = 0.3,
                ActivityWeight = 0.2,
                PowerReference = 1000,
                DevelopmentReference = 1000,
                ActivityReference = 1000,
                Scale = 1000
            },
            Combat = new CombatConfig { PreviewOddsThresholds = [2.0, 1.2, 0.8] }
        };

        private static InvalidOperationException Rejects(Action<GameConfig> break_)
        {
            var config = ValidConfig();
            break_(config);

            return Assert.Throws<InvalidOperationException>(() => GameConfigValidator.Validate(config));
        }

        [Fact]
        public void Validate_ShouldAcceptAWellFormedConfig()
        {
            var exception = Record.Exception(() => GameConfigValidator.Validate(ValidConfig()));

            Assert.Null(exception);
        }

        // ---------- Ключі ----------

        [Fact]
        public void Validate_ShouldRejectDuplicateBuildingKeys()
        {
            var error = Rejects(c => c.Buildings.Add(
                new BuildingConfig { Key = "farm", UpgradeCostGrowth = 1.45 }));

            Assert.Contains("duplicate", error.Message, StringComparison.OrdinalIgnoreCase);
        }

        /// <summary>
        /// Головна будівля гейтить усі інші, тож ані нуля, ані двох
        /// осмислено інтерпретувати не можна.
        /// </summary>
        [Fact]
        public void Validate_ShouldRejectMissingMainBuilding()
            => Rejects(c => c.Buildings.First(b => b.IsMainBuilding).IsMainBuilding = false);

        [Fact]
        public void Validate_ShouldRejectTwoMainBuildings()
            => Rejects(c => c.Buildings.First(b => b.Key == "farm").IsMainBuilding = true);

        // ---------- Економіка ----------

        /// <summary>Крива нижче одиниці робить апгрейди дешевшими з рівнем.</summary>
        [Fact]
        public void Validate_ShouldRejectShrinkingUpgradeCost()
            => Rejects(c => c.Buildings.First(b => b.Key == "farm").UpgradeCostGrowth = 0.9);

        [Fact]
        public void Validate_ShouldRejectUnknownStartingResource()
            => Rejects(c => c.StartingResources["gold"] = 100);

        /// <summary>
        /// Будівля не може коштувати ресурс, який відкривається пізніше за неї:
        /// стартового запасу вистачить ненадовго, і вона стане недосяжною.
        /// </summary>
        [Fact]
        public void Validate_ShouldRejectResourceUnlockedAfterTheBuildingThatCostsIt()
            => Rejects(c =>
            {
                c.Buildings.First(b => b.Key == "farm").RequiresMainBuildingLevel = 5;
                c.Buildings.First(b => b.Key == "warehouse").Cost =
                    [new ResourceCost { Resource = "food", Amount = 50 }];
            });

        /// <summary>Ресурс без сховища накопичується без ліміту.</summary>
        [Fact]
        public void Validate_ShouldRejectProducedResourceWithoutStorage()
            => Rejects(c => c.Buildings.First(b => b.Key == "warehouse").StoresResources = []);

        [Fact]
        public void Validate_ShouldRejectDefaultServerOutsideActiveOnes()
            => Rejects(c => c.DefaultServerId = 99);

        // ---------- Квести ----------

        [Fact]
        public void Validate_ShouldRejectMissingPrerequisite()
            => Rejects(c => c.Quests.Add(new QuestConfig { Key = "second", Prerequisite = "ghost" }));

        [Fact]
        public void Validate_ShouldRejectRewardWithUnknownResource()
            => Rejects(c => c.Quests.Add(new QuestConfig
            {
                Key = "first",
                Rewards = [new RewardConfig { Type = "Resource", Key = "mithril", Amount = 10 }]
            }));

        // ---------- Геометрія ----------

        /// <summary>Останнє кільце — це все за межею, тому меж на одну менше.</summary>
        [Fact]
        public void Validate_ShouldRejectMismatchedRingCounts()
            => Rejects(c => c.Map.Geometry.RingMultipliers = [2.0, 1.0]);

        [Fact]
        public void Validate_ShouldRejectNonIncreasingBoundaries()
            => Rejects(c => c.Map.Geometry.RingBoundaries = [0.5, 0.2]);

        [Fact]
        public void Validate_ShouldRejectBoundaryAboveOne()
            => Rejects(c => c.Map.Geometry.RingBoundaries = [0.2, 1.5]);

        [Fact]
        public void Validate_ShouldRejectFogMinAboveMax()
            => Rejects(c => c.Map.Geometry.FogMaxShare = 0.2);

        /// <summary>
        /// Порожня геометрія дозволена навмисно: мінімальні фікстури
        /// в тестах її не описують.
        /// </summary>
        [Fact]
        public void Validate_ShouldAllowEmptyGeometry()
        {
            var config = ValidConfig();
            config.Map.Geometry = new MapGeometryConfig();

            var exception = Record.Exception(() => GameConfigValidator.Validate(config));

            Assert.Null(exception);
        }

        // ---------- Рейтинг ----------

        [Fact]
        public void Validate_ShouldRejectWeightsThatDoNotSumToOne()
            => Rejects(c => c.Rating.PowerWeight = 0.9);

        [Fact]
        public void Validate_ShouldRejectNonPositiveReference()
            => Rejects(c => c.Rating.PowerReference = 0);

        [Fact]
        public void Validate_ShouldRejectNonPositiveScale()
            => Rejects(c => c.Rating.Scale = 0);

        // ---------- Прев'ю ----------

        /// <summary>Пороги йдуть від найсильнішої смуги до найслабшої.</summary>
        [Fact]
        public void Validate_ShouldRejectNonDecreasingPreviewThresholds()
            => Rejects(c => c.Combat.PreviewOddsThresholds = [0.8, 1.2]);
    }
}
