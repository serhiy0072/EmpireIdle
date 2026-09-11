using EmpireIdle.Domain.Enums;
using EmpireIdle.Domain.Services;
using EmpireIdle.Domain.Services.Config;

namespace EmpireIdle.Domain.Tests.Services
{
    /// <summary>
    /// Валідатор конфіга — єдине, що стоїть між битим JSON і грою.
    /// Кожен тест ламає рівно одну річ у валідному конфігу.
    /// </summary>
    public class GameConfigValidatorTests
    {
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

        // ---------- Герої ----------

        /// <summary>
        /// Валідний конфіг героїв: зала героїв, два класи, три тіри,
        /// два предмети еволюції, звичайний герой із ціною уламка.
        /// </summary>
        private static GameConfig WithHeroes()
        {
            var config = ValidConfig();

            // Зала героїв додається тут, а не у ValidConfig: базова фікстура
            // лишається мінімальною, а гейт потрібен лише героям
            config.Buildings.Add(new BuildingConfig { Key = "heroeshall", UpgradeCostGrowth = 1.45 });

            config.Items =
            [
                new ItemConfig { Key = "hero_essence_t2" },
                new ItemConfig { Key = "hero_essence_t3" }
            ];

            config.HeroSettings = new HeroesConfig
            {
                LevelsPerTier = 10,
                MaxTier = 3,
                MaxMarches = 8,
                TierStatMultipliers = [1.0, 1.35, 1.8],
                EvolutionItemKeys = ["hero_essence_t2", "hero_essence_t3"],
                OverflowGems = new Dictionary<string, int> { ["Common"] = 0, ["Rare"] = 15, ["Unique"] = 40 },
                BuildingKey = "heroeshall",
                Classes = ["warrior", "archer"]
            };

            config.Heroes =
            [
                new HeroConfig
                {
                    Key = "warrior_bran", Class = "warrior", Rank = Rarity.Common,
                    SummonShards = 10, ShardPriceGold = 1200
                },
                new HeroConfig { Key = "archer_lyra", Class = "archer", Rank = Rarity.Unique }
            ];

            return config;
        }

        private static InvalidOperationException RejectsHero(Action<GameConfig> break_)
        {
            var config = WithHeroes();
            break_(config);

            return Assert.Throws<InvalidOperationException>(() => GameConfigValidator.Validate(config));
        }

        [Fact]
        public void Validate_ShouldAcceptAValidHeroRoster()
            => GameConfigValidator.Validate(WithHeroes());

        /// <summary>
        /// Порожній ростер означає «конфіг героїв не описує» — мінімальні
        /// фікстури тестів не мусять заповнювати всю секцію.
        /// </summary>
        [Fact]
        public void Validate_ShouldIgnoreHeroRules_WhenTheRosterIsEmpty()
        {
            var config = WithHeroes();
            config.Heroes = [];
            config.HeroSettings.Classes = [];

            GameConfigValidator.Validate(config);
        }

        [Fact]
        public void Validate_ShouldRejectDuplicateHeroKeys()
            => RejectsHero(c => c.Heroes =
            [
                new HeroConfig { Key = "warrior_bran", Class = "warrior", SummonShards = 1, ShardPriceGold = 1 },
                new HeroConfig { Key = "warrior_bran", Class = "warrior", SummonShards = 1, ShardPriceGold = 1 }
            ]);

        /// <summary>
        /// Клас із друкарською помилкою дав би героя, якому не підходить
        /// жоден предмет — і виявилось би це вже в гравця.
        /// </summary>
        [Fact]
        public void Validate_ShouldRejectUnknownHeroClass()
            => RejectsHero(c => c.Heroes[1].Class = "paladin");

        [Fact]
        public void Validate_ShouldRejectEmptyClassRoster()
            => RejectsHero(c => c.HeroSettings.Classes = []);

        [Fact]
        public void Validate_ShouldRejectDuplicateClasses()
            => RejectsHero(c => c.HeroSettings.Classes = ["warrior", "warrior", "archer"]);

        /// <summary>Множників має бути рівно стільки, скільки тірів.</summary>
        [Fact]
        public void Validate_ShouldRejectTierMultiplierCountMismatch()
            => RejectsHero(c => c.HeroSettings.TierStatMultipliers = [1.0, 1.35]);

        /// <summary>
        /// Незростаючі множники означають, що еволюція піднімає лише стелю,
        /// і два герої різних тірів на тому самому рівні однакові.
        /// </summary>
        [Fact]
        public void Validate_ShouldRejectNonIncreasingTierMultipliers()
            => RejectsHero(c => c.HeroSettings.TierStatMultipliers = [1.0, 1.35, 1.35]);

        /// <summary>Переходів рівно на один менше, ніж тірів.</summary>
        [Fact]
        public void Validate_ShouldRejectEvolutionItemCountMismatch()
            => RejectsHero(c => c.HeroSettings.EvolutionItemKeys = ["hero_essence_t2"]);

        [Fact]
        public void Validate_ShouldRejectUnknownEvolutionItem()
            => RejectsHero(c => c.HeroSettings.EvolutionItemKeys = ["hero_essence_t2", "missing_essence"]);

        /// <summary>
        /// Звичайні герої — основний щоденний стік золота. Без ціни уламка
        /// вони роздавались би безкоштовно.
        /// </summary>
        [Fact]
        public void Validate_ShouldRejectCommonHeroWithoutShardPrice()
            => RejectsHero(c => c.Heroes[0].ShardPriceGold = 0);

        [Fact]
        public void Validate_ShouldRejectCommonHeroWithoutShardCount()
            => RejectsHero(c => c.Heroes[0].SummonShards = 0);

        /// <summary>Нуль маршів лишив би гравця з героями без доступу до карти.</summary>
        [Fact]
        public void Validate_ShouldRejectZeroMaxMarches()
            => RejectsHero(c => c.HeroSettings.MaxMarches = 0);

        /// <summary>Без будівлі героїв не було б де призивати.</summary>
        [Fact]
        public void Validate_ShouldRejectUnknownHeroBuilding()
            => RejectsHero(c => c.HeroSettings.BuildingKey = "ghosthall");

        /// <summary>
        /// Забутий ранг означав би, що дублікат понад стелю сузір'я
        /// зникає без сліду — саме те, чого ми уникали.
        /// </summary>
        [Fact]
        public void Validate_ShouldRejectMissingOverflowGemsForRank()
            => RejectsHero(c => c.HeroSettings.OverflowGems.Remove("Unique"));
    }
}
