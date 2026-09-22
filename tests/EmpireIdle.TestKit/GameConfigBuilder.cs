    using EmpireIdle.Domain.Enums;
    using EmpireIdle.Domain.Services;
    using EmpireIdle.Domain.Services.Config;

    namespace EmpireIdle.TestKit;

    /// <summary>
    /// Збирає GameConfig із секцій.
    ///
    /// Причина існування: GameCatalog проганяє повний GameConfigValidator, і кожна
    /// нова перевірка в ньому валила десятки тестів у файлах, які до неї стосунку
    /// не мають. Тепер секція описана один раз і валідна за побудовою.
    ///
    /// Тест бере лише те, що йому потрібно: секція, якої не просили, лишається
    /// порожньою, і валідатор її пропускає.
    /// </summary>
    public sealed class GameConfigBuilder
    {
        private readonly GameConfig _config = new();

        /// <summary>Ресурси. Без них будь-яка вартість посилається в нікуди.</summary>
        public GameConfigBuilder WithResources(params string[] keys)
        {
            var actual = keys.Length > 0 ? keys : TestKeys.AllResources;

            _config.Resources = actual.Select(k => new ResourceConfig {
                Key = k,
                DisplayName = $"Resource {k}",
                Icon = k,
            }).ToList();

            return this;
        }

        /// <summary>
        /// Ратуша плюс перелічені будівлі. Ратуша додається завжди: без головної
        /// будівлі валідатор не пропускає нічого.
        /// </summary>
        public GameConfigBuilder WithBuildings(params string[] keys)
        {
            EnsureResources();

            _config.Buildings =
            [
                new BuildingConfig
                {
                    Key = TestKeys.Townhall,
                    DisplayName = $"Building {TestKeys.Townhall}",
                    IsMainBuilding = true,
                    UpgradeCostGrowth = 1.45,
                    BaseBuildMinutes = 5,
                    BuildTimeGrowth = 1.5,
                    RequiresMainBuildingLevel = 0,
                    Cost = [new ResourceCost { Resource = TestKeys.Wood, Amount = 100 }]
                }
            ];

            foreach (var key in keys.Where(k => k != TestKeys.Townhall))
                _config.Buildings.Add(Building(key));

            return this;
        }

        /// <summary>Юніти з бойовими статами й швидкістю.</summary>
        public GameConfigBuilder WithUnits(Action<UnitConfig>? tune = null)
        {
            EnsureResources();

            _config.Units =
            [
                Unit(TestKeys.Infantry, attack: 10, defence: 12, speed: 4),
                Unit(TestKeys.Archer, attack: 14, defence: 8, speed: 6),
                Unit(TestKeys.Cavalry, attack: 16, defence: 10, speed: 10)
            ];

            if (tune is not null)
                foreach (var unit in _config.Units)
                    tune(unit);

            return this;
        }

        /// <summary>Бойова секція зі смугами втрат за замовчуванням.</summary>
        public GameConfigBuilder WithCombat(Action<CombatConfig>? tune = null)
        {
            _config.Combat = new CombatConfig();
            tune?.Invoke(_config.Combat);

            return this;
        }

        /// <summary>Мапа з одним прохідним рельєфом.</summary>
        public GameConfigBuilder WithMap(Action<MapConfig>? tune = null)
        {
            _config.Map = new MapConfig
            {
                Width = 200,
                Height = 200,
                TerrainSeed = 1,
                Terrains =
                [
                    new TerrainConfig
                    {
                        Type = TestKeys.Terrain,
                        Weight = 1,
                        Passable = true,
                        MoveCost = 1.0,
                        Habitable = true
                    }
                ]
            };

            tune?.Invoke(_config.Map);

            return this;
        }

        /// <summary>
        /// Ростер: звичайний воїн, унікальний маг і третій без пасивок —
        /// для перевірок «бонусу немає».
        /// </summary>
        public GameConfigBuilder WithHeroes(Action<HeroesConfig>? tune = null,
            params HeroPassiveConfig[] passives)
        {
            EnsureResources();
            EnsureBuildings(TestKeys.Hall, TestKeys.Hospital);
            EnsureItems(TestKeys.EssenceT2, TestKeys.EssenceT3);

            _config.HeroSettings = new HeroesConfig
            {
                BuildingKey = TestKeys.Hall,
                HealBuildingKey = TestKeys.Hospital,

                LevelsPerTier = 10,
                MaxTier = 3,
                MaxMarches = 8,
                MaxConstellation = 6,
                BaseLevelUpMinutes = 4,
                DefaultMarchSpeed = 6,
                HealCostPerLevel = [new ResourceCost { Resource = TestKeys.Food, Amount = 40 }],
                TierStatMultipliers = [1.0, 1.5, 2.0],
                EvolutionItemKeys = [TestKeys.EssenceT2, TestKeys.EssenceT3],
                OverflowSeals = new Dictionary<string, int> { ["Common"] = 0, ["Rare"] = 15, ["Unique"] = 40 },
                Classes = ["warrior", "knight", "archer", "mage"]
            };

            _config.Heroes =
            [
                Hero(TestKeys.CommonHero, "warrior", Rarity.Common, attack: 100, passives),
                Hero(TestKeys.UniqueHero, "mage", Rarity.Unique, attack: 105),
                Hero(TestKeys.PlainHero, "warrior", Rarity.Common, attack: 40)
            ];

            tune?.Invoke(_config.HeroSettings);

            return this;
        }

        /// <summary>
        /// Спорядження: дві зброї воїна, набір із чотирьох артефактів
        /// і один артефакт поза набором.
        /// </summary>
        public GameConfigBuilder WithEquipment(Action<EquipmentConfig>? tune = null)
        {
            EnsureResources();
            EnsureBuildings(TestKeys.Forge);

            _config.Equipment = new EquipmentConfig
            {
                ArtifactSlots = 4,
                MaxEnhancement = 20,
                EnhancementBonusPerLevel = 0.1,
                ForgeBuildingKey = TestKeys.Forge,
                EnhanceBaseGold = 200,
                EnhanceCostGrowth = 1.35,
                SafeEnhancementLevel = 5,
                SuccessDropPerLevel = 0.05,
                MinSuccessChance = 0.25,
                BreakChanceOnFailure = 0.2,
                RepairGemsBase = 20, RepairGemsPerLevel = 8,
                ArtifactBaseStats = 2,
                ArtifactStatLevels = [4, 8],
                ArtifactUpgradeLevels = [12, 16, 20],
                DoubleUpgradeChance = 0.1,
                ArtifactStats =
                [
                    new ArtifactStatConfig { Stat = "Attack", Min = 4, Max = 12, UpgradeMin = 1, UpgradeMax = 3 },
                    new ArtifactStatConfig { Stat = "Defense", Min = 5, Max = 14, UpgradeMin = 1, UpgradeMax = 4 },
                    new ArtifactStatConfig { Stat = "Health", Min = 20, Max = 60, UpgradeMin = 5, UpgradeMax = 15 }
                ],
                ArtifactRarityMultipliers = new Dictionary<string, double>
                {
                    ["Common"] = 1.0,
                    ["Rare"] = 1.4,
                    ["Unique"] = 2.0
                },
                SetBonuses =
                [
                    new SetBonusConfig
                    {
                        SetKey = TestKeys.SetKey,
                        RequiredPieces = 4,
                        Stats = new Dictionary<string, double> { ["Attack"] = 25 }
                    }
                ]
            };

            _config.Items.AddRange(
            [
                WeaponItem(TestKeys.Weapon, attack: 12, price: 500),
                WeaponItem(TestKeys.BetterWeapon, attack: 18, price: 1000),
                ArtifactItem(TestKeys.Artifact, TestKeys.SetKey),
                ArtifactItem(TestKeys.SecondArtifact, TestKeys.SetKey),
                ArtifactItem(TestKeys.ThirdArtifact, TestKeys.SetKey),
                ArtifactItem(TestKeys.FourthArtifact, TestKeys.SetKey),
                ArtifactItem(TestKeys.LooseArtifact, setKey: null)
            ]);

            tune?.Invoke(_config.Equipment);

            return this;
        }

        public GameConfig Build() => _config;

        /// <summary>Конфіг разом із каталогом — тобто вже провалідований.</summary>
        public GameCatalog BuildCatalog() => new(Build());

        // ---------- внутрішні фабрики ----------

        private static BuildingConfig Building(string key) => new()
        {
            Key = key,
            DisplayName = $"Building {key}",
            UpgradeCostGrowth = 1.45,
            BaseBuildMinutes = 5,
            BuildTimeGrowth = 1.5,
            RequiresMainBuildingLevel = 0,
            Cost = [new ResourceCost { Resource = TestKeys.Gold, Amount = 10 }],
            StoresResources = key == TestKeys.Warehouse ? [.. TestKeys.AllResources] : []
        };

        private static UnitConfig Unit(string key, double attack, double defence, double speed) => new()
        {
            Key = key,
            DisplayName = $"Unit {key}",
            Stats = new Dictionary<string, double>
            {
                ["Attack"] = attack,
                ["Defense"] = defence,
                ["Speed"] = speed
            },
            Cost = [new ResourceCost { Resource = TestKeys.Food, Amount = 10 }]
        };

        private static HeroConfig Hero(string key, string heroClass, Rarity rank, double attack,
            HeroPassiveConfig[]? passives = null) => new()
            {
                Key = key,
                Class = heroClass,
                Rank = rank,
                SummonShards = 10,
                ShardPriceGold = 100,
                DisplayName = $"Hero {key}",
                Description = $"Test hero {key}",
                BaseStats = new Dictionary<string, double>
                {
                    ["Attack"] = attack,
                    ["Defense"] = Math.Round(attack * 0.4)
                },
                // Швидкість — окреме поле: у BaseStats вона множилась би тіром і йшла в Power
                Speed = 4,
                StatGrowth = new Dictionary<string, double> { ["Attack"] = 10, ["Defense"] = 4 },
                LevelUpCosts =
                [
                    new HeroLevelCostBand
                    {
                        FromLevel = 1,
                        Cost = [new ResourceCost { Resource = TestKeys.Gold, Amount = 100 }]
                    }
                ],
                Passives = [.. passives ?? []]
            };

        private static ItemConfig WeaponItem(string key, double attack, int price) => new()
        {
            Key = key,
            Type = "equipment",
            Slot = EquipmentSlot.Weapon,
            WeaponClasses = ["warrior"],
            DisplayName = $"Item {key}",
            Description = $"Test item {key}",
            BaseStats = new Dictionary<string, double> { ["Attack"] = attack },
            PriceGold = price
        };

        private static ItemConfig ArtifactItem(string key, string? setKey) => new()
        {
            Key = key,
            Type = "equipment",
            DisplayName = $"Item {key}",
            Description = $"Test item {key}",
            Slot = EquipmentSlot.Artifact,
            SetKey = setKey
        };

        private void EnsureResources()
        {
            if (_config.Resources.Count == 0)
                WithResources();
        }

        /// <summary>
        /// Секції залежні: героям потрібна зала, спорядженню — кузня.
        /// Добудовуємо мовчки, щоб тест не мусив пам'ятати порядок викликів.
        /// </summary>
        private void EnsureBuildings(params string[] keys)
        {
            if (_config.Buildings.Count == 0)
                WithBuildings(keys);

            foreach (var key in keys.Where(k => _config.Buildings.All(b => b.Key != k)))
                _config.Buildings.Add(Building(key));
        }

        private void EnsureItems(params string[] keys)
        {
            foreach (var key in keys.Where(k => _config.Items.All(i => i.Key != k)))
            _config.Items.Add(new ItemConfig
            {
                Key = key,
                Type = "evolution",
                DisplayName = $"Item {key}",
                Description = $"Test item {key}"
            });
    }
    }
