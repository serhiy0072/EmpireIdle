using EmpireIdle.Domain.Enums;
using EmpireIdle.Domain.Services;
using EmpireIdle.Domain.Services.Config;
using EmpireIdle.TestKit;

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
                new BuildingConfig { Key = "warehouse", StoresResources = ["food"], BaseStorage = 1000, UpgradeCostGrowth = 1.45 },
                new BuildingConfig { Key = "hospital" }
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

        // ---------- Пороги відкриття ----------

        /// <summary>
        /// Поріг вище за ратушу, яку взагалі можна збудувати (3 × 10 = 30),
        /// робить вміст недосяжним назавжди — так було з данжем на 32.
        /// </summary>
        [Fact]
        public void Validate_ShouldRejectABuildingUnlockedAboveTheTownHallCeiling()
            => Assert.Contains("building farm (31)", Rejects(c => c.Buildings.Single(b => b.Key == "farm").RequiresMainBuildingLevel = 31).Message);

        [Fact]
        public void Validate_ShouldRejectAResourceUnlockedAboveTheTownHallCeiling()
            => Assert.Contains("resource food (31)", Rejects(c => c.Resources.Single().RequiresMainBuildingLevel = 31).Message);

        /// <summary>Сама стеля — ще досяжна: максимальна ратуша її відкриває.</summary>
        [Fact]
        public void Validate_ShouldAcceptAThresholdExactlyAtTheCeiling()
        {
            var config = ValidConfig();
            config.Buildings.Single(b => b.Key == "hospital").RequiresMainBuildingLevel = 30;

            Assert.Null(Record.Exception(() => GameConfigValidator.Validate(config)));
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
                .. TestKit.UniversalShards.All(),
                new ItemConfig { Key = "hero_essence_t2" },
                new ItemConfig { Key = "hero_essence_t3" }
            ];

            config.HeroSettings = new HeroesConfig
            {
                MaxMarches = 8,
                TierGrowth = 1.10,
                StarPartCosts = [[1, 1, 2, 2, 2, 2], [5, 5, 5, 5, 5, 5], [10, 10, 10, 10, 10, 10], [20, 20, 20, 20, 20, 20], [40, 40, 40, 40, 40, 40], [100, 100, 100, 100, 100, 100]],
                EvolutionItemKeys = ["hero_essence_t2", "hero_essence_t3"],
                BuildingKey = "heroeshall",
                HealBuildingKey = "hospital",
                HealCostPerLevel = [new ResourceCost { Resource = "food", Amount = 40 }],
                Classes = ["warrior", "archer"]
            };

            config.Heroes =
            [
                new HeroConfig
                    {
                        Key = "warrior_bran", Class = "warrior", Rank = Rarity.Common,
},
                    new HeroConfig
                    {
                        Key = "archer_lyra", Class = "archer", Rank = Rarity.Unique,
}
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
                new HeroConfig { Key = "warrior_bran", Class = "warrior" },
                new HeroConfig { Key = "warrior_bran", Class = "warrior" }
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

        /// <summary>Тір без росту — новий тір не сильніший за старий (GDD §6.1).</summary>
        [Fact]
        public void Validate_ShouldRejectATierGrowthOfOneOrLess()
            => RejectsHero(c => c.HeroSettings.TierGrowth = 1.0);

        /// <summary>Штраф понад 1 зробив би піднятого героя сильнішим за рідного.</summary>
        [Fact]
        public void Validate_ShouldRejectAnEvolutionPenaltyAboveOne()
            => RejectsHero(c => c.HeroSettings.EvolutionPenalty = 1.05);

        /// <summary>Рідний тір, якого немає серед описаних тірів, — героя ніколи б не видали.</summary>
        [Fact]
        public void Validate_ShouldRejectANativeTierAboveTheHighestTier()
            => RejectsHero(c => c.Heroes[0].NativeTier = 4);

        [Fact]
        public void Validate_ShouldRejectUnknownEvolutionItem()
            => RejectsHero(c => c.HeroSettings.EvolutionItemKeys = ["hero_essence_t2", "missing_essence"]);

        /// <summary>Зірка з неповним списком цін упала б на першій же частинці без ціни (GDD §6.1).</summary>
        [Fact]
        public void Validate_ShouldRejectAStarWithoutEveryPartCost()
            => RejectsHero(c => c.HeroSettings.StarPartCosts[5] = [100, 100]);

        /// <summary>Безкоштовна частинка — зірки задарма.</summary>
        [Fact]
        public void Validate_ShouldRejectAFreeStarPart()
            => RejectsHero(c => c.HeroSettings.StarPartCosts[0][0] = 0);

        /// <summary>Без універсального осколка рідкості надлишок прокачаного героя нікуди подіти.</summary>
        [Fact]
        public void Validate_ShouldRejectARosterRarityWithoutAUniversalShard()
            => RejectsHero(c => c.Items.RemoveAll(i => i.Key == TestKit.UniversalShards.Unique));

        /// <summary>З найвищої рідкості міняти нікуди.</summary>
        [Fact]
        public void Validate_ShouldRejectAnUpgradeFromTheTopRarity()
            => RejectsHero(c => c.HeroSettings.UniversalShardUpgrade["Unique"] = 500);

        /// <summary>Нуль маршів лишив би гравця з героями без доступу до карти.</summary>
        [Fact]
        public void Validate_ShouldRejectZeroMaxMarches()
            => RejectsHero(c => c.HeroSettings.MaxMarches = 0);

        /// <summary>Без будівлі героїв не було б де призивати.</summary>
        [Fact]
        public void Validate_ShouldRejectUnknownHeroBuilding()
            => RejectsHero(c => c.HeroSettings.BuildingKey = "ghosthall");

        /// <summary>Крива без росту — пізні рівні коштували б як перші (GDD §6.1).</summary>
        [Fact]
        public void Validate_ShouldRejectANonPositiveExperienceCurve()
            => RejectsHero(c => c.HeroSettings.ExperienceExponent = 0);

        /// <summary>Штраф 100% спалив би весь досвід — скидання стало б покаранням, а не виправленням.</summary>
        [Fact]
        public void Validate_ShouldRejectAResetPenaltyOfOne()
            => RejectsHero(c => c.HeroSettings.ResetPenalty = 1.0);

        [Fact]
        public void Validate_ShouldRejectAMaxLevelBelowOne()
            => RejectsHero(c => c.HeroSettings.MaxLevel = 0);

        [Fact]
        public void Validate_ShouldRejectAnUnknownHealBuilding()
            => RejectsHero(c => c.HeroSettings.HealBuildingKey = "infirmary");

        [Fact]
        public void Validate_ShouldRejectFreeHealing()
            => RejectsHero(c => c.HeroSettings.HealCostPerLevel.Clear());

        [Fact]
        public void Validate_ShouldRejectHealCostInAnUnknownResource()
            => RejectsHero(c => c.HeroSettings.HealCostPerLevel =
                [new ResourceCost { Resource = "mithril", Amount = 10 }]);

        // ---------- Смуги втрат ----------

        [Fact]
        public void Validate_ShouldRejectABandWithMinAboveMax()
        {
            var error = Rejects(c => c.Combat.AttackerWinLosses = new LossBand { Min = 0.4, Max = 0.2 });

            Assert.Contains("AttackerWinLosses", error.Message);
        }

        [Fact]
        public void Validate_ShouldRejectABandAboveOne()
        {
            var error = Rejects(c => c.Combat.DefenderLossLosses = new LossBand { Min = 0.3, Max = 1.2 });

            Assert.Contains("DefenderLossLosses", error.Message);
        }

        [Fact]
        public void Validate_ShouldRejectANegativeBand()
        {
            var error = Rejects(c => c.Combat.DefenderWinLosses = new LossBand { Min = -0.1, Max = 0.2 });

            Assert.Contains("DefenderWinLosses", error.Message);
        }

        /// <summary>
        /// Головна перевірка: нижня межа програшу під верхньою межею перемоги
        /// означає, що за певного співвідношення сил програти дешевше, ніж
        /// перемогти, і оптимальною стратегією стає навмисна поразка.
        /// </summary>
        [Fact]
        public void Validate_ShouldRejectInvertedAttackerBands()
        {
            var error = Rejects(c =>
            {
                c.Combat.AttackerWinLosses = new LossBand { Min = 0.02, Max = 0.50 };
                c.Combat.AttackerLossLosses = new LossBand { Min = 0.35, Max = 0.60 };
            });

            Assert.Contains("AttackerLossLosses", error.Message);
        }

        [Fact]
        public void Validate_ShouldRejectInvertedDefenderBands()
        {
            var error = Rejects(c =>
            {
                c.Combat.DefenderWinLosses = new LossBand { Min = 0.02, Max = 0.40 };
                c.Combat.DefenderLossLosses = new LossBand { Min = 0.25, Max = 0.50 };
            });

            Assert.Contains("DefenderLossLosses", error.Message);
        }

        /// <summary>Вітрина обіцяє унікальний посох, а в Items він звичайний — гравець отримав би не те, що бачив.</summary>
        [Fact]
        public void Validate_ShouldRejectABannerDrop_WhoseRarityDiffersFromTheItem()
        {
            // Секція спорядження має власні вимоги — беремо валідну з TestKit і ламаємо лише рідкість лота
            var config = new GameConfigBuilder().WithResources().WithBuildings().WithHeroes().WithEquipment().Build();
            config.Shop.Banners =
            [
                new BannerConfig
                {
                    Key = "forge", DisplayName = "Forge", Kind = BannerKind.Weapon, PityGroup = "weapon", PriceGems = 100,
                    RarePity = 10, UniquePity = 50,
                    Drops =
                    [
                        new BannerDropConfig
                        {
                            Key = TestKeys.Weapon, DisplayName = "Weapon", Rarity = Rarity.Unique, Kind = BannerKind.Weapon, Weight = 1,
                            Rewards = [new RewardConfig { Type = "Equipment", Key = TestKeys.Weapon, Amount = 1 }]
                        }
                    ]
                }
            ];

            var error = Assert.Throws<InvalidOperationException>(() => GameConfigValidator.Validate(config));

            Assert.Contains(TestKeys.Weapon, error.Message);
            Assert.Contains("Unique", error.Message);
        }

        [Fact]
        public void Validate_ShouldRejectAShopItem_ThatIsNotAnItem()
        {
            var error = Rejects(c => c.Shop.Items = [new ShopItemConfig { ItemKey = "no_such_item", PriceGems = 100 }]);

            Assert.Contains("no_such_item", error.Message);
        }

        /// <summary>Зброя продається кузнею за золото — крамниця за gems її не дублює.</summary>
        [Fact]
        public void Validate_ShouldRejectAShopItem_ThatIsEquipment()
        {
            var error = Rejects(c =>
            {
                c.Items = [new ItemConfig { Key = "sword_iron", DisplayName = "Sword", Description = "", Type = "equipment", Slot = EquipmentSlot.Weapon }];
                c.Shop.Items = [new ShopItemConfig { ItemKey = "sword_iron", PriceGems = 100 }];
            });

            Assert.Contains("equipment", error.Message);
        }

        [Fact]
        public void Validate_ShouldAcceptAShopItem_ThatSellsAnExistingConsumable()
        {
            var config = ValidConfig();
            config.Items = [.. TestKit.UniversalShards.All(), new ItemConfig { Key = "hero_essence_t2", DisplayName = "Essence", Description = "", Type = "evolution" }];
            config.Shop.Items = [new ShopItemConfig { ItemKey = "hero_essence_t2", PriceGems = 300 }];

            var exception = Record.Exception(() => GameConfigValidator.Validate(config));

            Assert.Null(exception);
        }

        /// <summary>Дотик межі — не інверсія: 0.35 і 0.35 сходяться, не перетинаються.</summary>
        [Fact]
        public void Validate_ShouldAcceptTouchingBands()
        {
            var config = ValidConfig();
            config.Combat.AttackerWinLosses = new LossBand { Min = 0.02, Max = 0.35 };
            config.Combat.AttackerLossLosses = new LossBand { Min = 0.35, Max = 0.60 };

            var exception = Record.Exception(() => GameConfigValidator.Validate(config));

            Assert.Null(exception);
        }
    }
}
