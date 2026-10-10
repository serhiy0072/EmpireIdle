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
        /// Ростер: звичайний воїн, унікальний маг і третій без вмінь —
        /// для перевірок «бонусу немає». Склад вмінь за рідкістю не задано, тож валідатор
        /// його не перевіряє: героям досить тих вмінь, які потрібні тесту.
        /// </summary>
        public GameConfigBuilder WithHeroes(Action<HeroesConfig>? tune = null,
            params HeroSkillConfig[] passives)
        {
            EnsureResources();
            EnsureBuildings(TestKeys.Hall, TestKeys.Hospital);
            EnsureItems(TestKeys.EssenceT2, TestKeys.EssenceT3);
            _config.Items.AddRange(UniversalShards.All().Where(u => _config.Items.All(i => i.Key != u.Key)));

            _config.HeroSettings = new HeroesConfig
            {
                BuildingKey = TestKeys.Hall,
                HealBuildingKey = TestKeys.Hospital,

                MaxMarches = 8,
                StarPartCosts = [[1, 1, 2, 2, 2, 2], [5, 5, 5, 5, 5, 5], [10, 10, 10, 10, 10, 10], [20, 20, 20, 20, 20, 20], [40, 40, 40, 40, 40, 40]],
                DefaultMarchSpeed = 6,
                HealCostPerLevel = [new ResourceCost { Resource = TestKeys.Food, Amount = 40 }],
                TierGrowth = 1.5,
                EvolutionItemKeys = [TestKeys.EssenceT2, TestKeys.EssenceT3],
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

            _config.Equipment = DefaultEquipment(TestKeys.Forge);
            _config.Equipment.SetBonuses =
            [
                new SetBonusConfig
                {
                    SetKey = TestKeys.SetKey,
                    RequiredPieces = 4,
                    Stats = new Dictionary<string, double> { ["Attack"] = 25 }
                }
            ];

            _config.Items.AddRange(
            [
                ArtifactItem(TestKeys.Artifact, TestKeys.SetKey, "necklace"),
                ArtifactItem(TestKeys.SecondArtifact, TestKeys.SetKey, "ring"),
                ArtifactItem(TestKeys.ThirdArtifact, TestKeys.SetKey, "crown"),
                ArtifactItem(TestKeys.FourthArtifact, TestKeys.SetKey, "belt"),
                ArtifactItem(TestKeys.LooseArtifact, setKey: null, "ring"),
                new ItemConfig { Key = RepairKit, Type = "repairkit", DisplayName = "Ремкомплект", Description = "r" }
            ]);

            tune?.Invoke(_config.Equipment);

            return this;
        }

        /// <summary>
        /// Правила артефактів із числами гри (GDD §9.12), без наборів: слоти з бонусами,
        /// база, крива рівня, ступені й шанси заточки. Спільні для всіх тестових конфігів,
        /// щоб таблиці не розходились між тестами.
        /// </summary>
        public static EquipmentConfig DefaultEquipment(string forgeKey)
            => new()
            {
                ArtifactSlots =
                [
                    Slot("necklace", "Намисто", ["Attack", "UnitAttack"], ("CritChance", 25), ("AttackSpeed", 35), ("AttackPercent", 40)),
                    Slot("crown", "Корона", ["Defense", "UnitDefense"], ("CritPower", 25), ("CooldownReduction", 10), ("SkillDamage", 30), ("Lifesteal", 30)),
                    Slot("ring", "Кільце", ["Attack", "Defense"], ("CritChance", 25), ("BlockChance", 22), ("BlockPower", 22), ("DamageReduction", 14), ("DefensePercent", 22)),
                    Slot("belt", "Пояс", ["UnitAttack", "UnitDefense"], ("CritPower", 25), ("UnitCritChance", 25), ("UnitHealthPercent", 25), ("HealthPercent", 25))
                ],
                MaxLevel = 80,
                LevelExperienceBase = 100,
                LevelExperienceCoefficient = 2.2,
                LevelExperienceExponent = 1.55,
                FeedExperience = new Dictionary<Rarity, int> { [Rarity.Common] = 100, [Rarity.Rare] = 300, [Rarity.Unique] = 1000 },
                ArtifactBase = new Dictionary<Rarity, ArtifactBaseConfig>
                {
                    [Rarity.Common] = new() { Hero = 10, HeroPerLevel = 2, Unit = 40, UnitPerLevel = 8 },
                    [Rarity.Rare] = new() { Hero = 16, HeroPerLevel = 3, Unit = 64, UnitPerLevel = 12 },
                    [Rarity.Unique] = new() { Hero = 25, HeroPerLevel = 4, Unit = 100, UnitPerLevel = 16 }
                },
                HeroStatPower = 1.0,
                UnitStatPower = 0.25,
                ArtifactBonuses =
                [
                    Bonus("Attack", 0, 5, 10, 15, 20),
                    Bonus("Defense", 0, 5, 10, 15, 20),
                    Bonus("UnitAttack", 0, 5, 10, 15, 20),
                    Bonus("UnitDefense", 0, 5, 10, 15, 20),
                    Bonus("CritChance", 10, 1, 2, 3, 4),
                    Bonus("CritPower", 2.5, 4, 8, 12, 16),
                    Bonus("SkillDamage", 3.5, 3, 6, 9, 12),
                    Bonus("AttackSpeed", 5, 2, 4, 6, 8),
                    Bonus("CooldownReduction", 9, 2, 4, 6, 8),
                    Bonus("Lifesteal", 8, 1, 2, 3, 4),
                    Bonus("BlockChance", 8, 1, 2, 3, 4),
                    Bonus("BlockPower", 2, 4, 8, 12, 16),
                    Bonus("DamageReduction", 10, 1, 2, 3, 4),
                    Bonus("AttackPercent", 3, 3, 6, 9, 12),
                    Bonus("DefensePercent", 3, 3, 6, 9, 12),
                    Bonus("HealthPercent", 3, 3, 6, 9, 12),
                    Bonus("UnitCritChance", 8, 1, 2, 3, 4),
                    Bonus("UnitHealthPercent", 3, 3, 6, 9, 12)
                ],
                BonusStepChances = [0.4, 0.3, 0.2, 0.1],
                MaxMastery = 20,
                ForgeBuildingKey = forgeKey,
                MasteryBaseGold = 400,
                MasteryCostGrowth = 1.33,
                MasterySuccessChances = [1, 1, 1, 1, 1, 0.9, 0.85, 0.8, 0.75, 0.7, 0.65, 0.6, 0.55, 0.5, 0.45, 0.4, 0.35, 0.3, 0.27, 0.25],
                MasteryBreakChances = [0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0.1, 0.1, 0.1, 0.1, 0.1, 0.2, 0.2, 0.2, 0.2, 0.2],
                BrokenStatShare = 0.5,
                RepairGemsPerMastery = 50,
                RepairKitItemKey = RepairKit
            };

        /// <summary>Ключ ремкомплекта в DefaultEquipment; сам предмет додає WithEquipment.</summary>
        public const string RepairKit = "repair_kit";

        private static ArtifactSlotConfig Slot(string key, string name, List<string> fixedBonuses,
            params (string Stat, double Weight)[] pool)
            => new()
            {
                Key = key,
                DisplayName = name,
                FixedBonuses = fixedBonuses,
                RandomBonuses = pool.Select(e => new ArtifactPoolEntryConfig { Stat = e.Stat, Weight = e.Weight }).ToList()
            };

        private static ArtifactBonusConfig Bonus(string stat, double powerPerPercent, params double[] steps)
            => new() { Stat = stat, Steps = steps.ToList(), PowerPerPercent = powerPerPercent };

        /// <summary>
        /// Данжі: один данж із хвилею й босом, набір артефактів на кожну
        /// рідкість (валідатор вимагає всі три) і одне активне вміння
        /// звичайному героєві.
        /// </summary>
        public GameConfigBuilder WithDungeons(Action<DungeonsConfig>? tune = null)
        {
            EnsureResources();

            if (_config.Heroes.Count == 0)
                WithHeroes();

            if (_config.Equipment.SetBonuses.Count == 0)
                WithEquipment();

            var common = _config.Heroes.First(h => h.Key == TestKeys.CommonHero);

            common.Skills.RemoveAll(s => s.Kind == SkillKind.Active);
            common.Skills.Add(new HeroSkillConfig
            {
                Key = TestKeys.DungeonAbility,
                DisplayName = "Розтин",
                Half = SkillHalf.Attack,
                Kind = SkillKind.Active,
                Troops = new SkillTroopBonusConfig { Stat = "Attack", Percents = [0, 0, 0, 0, 0, 0] },
                Battle = new SkillBattleConfig
                {
                    Target = AbilityTarget.SingleEnemy,
                    Cooldown = 2,
                    DamageMultiplier = 1.8,
                    LevelScale = [1, 1.2, 1.4, 1.6, 1.8, 2]
                }
            });

            foreach (var rarity in Enum.GetNames<Rarity>())
            {
                var setKey = $"{TestKeys.DungeonSetKey}_{rarity.ToLowerInvariant()}";

                foreach (var piece in DungeonSetPieces)
                    _config.Items.Add(ArtifactItem($"{setKey}_{piece}", setKey, piece));

                _config.Equipment.SetBonuses.Add(new SetBonusConfig
                {
                    SetKey = setKey,
                    RequiredPieces = 4,
                    Stats = new Dictionary<string, double> { ["Attack"] = 10 }
                });
            }

            // Валідатор вимагає опис родини для набору кожного данжу
            _config.Equipment.ArtifactSets.Add(new ArtifactSetConfig
            {
                Key = TestKeys.DungeonSetKey,
                DisplayName = "Набір Ями",
                Tier = 1,
                FocusStats = ["Attack", "Health"]
            });

            _config.Dungeons = new DungeonsConfig
            {
                BaseWaves = 1,
                BaseCritChance = 0,
                FrontLineClasses = ["warrior"],
                LevelPowerMultipliers = [1.0, 1.8, 3.2],
                LevelRewardMultipliers = [1.0, 2.0, 3.5],
                Dungeons =
                [
                    new DungeonConfig
                    {
                        Key = TestKeys.Dungeon,
                        DisplayName = "Яма",
                        ArtifactSetKey = TestKeys.DungeonSetKey,
                        RequiresMainBuildingLevel = 1,
                        Waves = [DungeonEnemy(TestKeys.DungeonEnemy, "Громило")],
                        Boss = [DungeonEnemy(TestKeys.DungeonBoss, "Наглядач")],
                        Reward = [new ResourceCost { Resource = TestKeys.Gold, Amount = 100 }]
                    }
                ]
            };

            tune?.Invoke(_config.Dungeons);

            return this;
        }

        /// <summary>
        /// Звірі (GDD §5.10): звіринець на одне місце за рівень, монстр, з якого приручається
        /// вовк, і корм-предмет. Будівлі задає WithBuildings, тож звіринець додається поверх них.
        /// </summary>
        public GameConfigBuilder WithBeasts(Action<BeastsConfig>? tune = null)
        {
            if (_config.Buildings.Count == 0)
                WithBuildings();

            var pen = Building(TestKeys.BeastPen);
            pen.BeastCapacityPerLevel = 1;
            _config.Buildings.Add(pen);

            _config.Monsters.Add(new MonsterConfig
            {
                Key = TestKeys.BeastMonster,
                DisplayName = "Вовки",
                MinLevel = 1,
                MaxLevel = 10,
                Units = [],
                Rewards = []
            });

            _config.Items.Add(new ItemConfig
            {
                Key = TestKeys.BeastFeed,
                DisplayName = "Корм",
                Description = "Корм для звірів",
                Type = "feed"
            });

            _config.Beasts = new BeastsConfig
            {
                TameChancePerPenLevel = 0.01,
                MaxTameChanceMultiplier = 2,
                PityWins = 10,
                MaxRank = 5,
                LevelsPerRank = 10,
                FeedItemKey = TestKeys.BeastFeed,
                BaseExperience = 100,
                ExperienceGrowth = 1.25,
                Types =
                [
                    new BeastConfig
                    {
                        Key = TestKeys.Beast, DisplayName = "Вовк", MonsterKey = TestKeys.BeastMonster, TameChance = 0.2,
                        Effect = EffectTarget.Attack, BaseBonus = 0.15, BonusPerLevel = 0.01,
                        DurationMinutes = 120, CooldownMinutes = 480, ActivationFood = 100
                    }
                ]
            };

            tune?.Invoke(_config.Beasts);

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
            HeroSkillConfig[]? passives = null) => new()
            {
                Key = key,
                Class = heroClass,
                Rank = rank,
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
                Skills = [.. passives ?? []]
            };

        /// <summary>Набір данжу має рівно стільки частин, скільки вимагає бонус набору.</summary>
        /// <summary>Частини набору данжу — по одній на кожен тип слота.</summary>
        private static readonly string[] DungeonSetPieces = ["necklace", "crown", "ring", "belt"];

        /// <summary>Ворог данжу з такою горою здоров'я, щоб бій не скінчився сам.</summary>
        private static DungeonEnemyConfig DungeonEnemy(string key, string displayName) => new()
        {
            Key = key,
            DisplayName = displayName,
            Line = BattleLine.Front,
            Attack = 40,
            Defense = 20,
            Health = 100_000,
            Speed = 20
        };

        private static ItemConfig ArtifactItem(string key, string? setKey, string artifactSlot) => new()
        {
            Key = key,
            Type = "equipment",
            DisplayName = $"Item {key}",
            Description = $"Test item {key}",
            Slot = EquipmentSlot.Artifact,
            ArtifactSlot = artifactSlot,
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
