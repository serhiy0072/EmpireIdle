using EmpireIdle.Domain.Enums;
using EmpireIdle.Domain.Services;
using EmpireIdle.Domain.Services.Config;

namespace EmpireIdle.Application.Tests.Heroes;

/// <summary>
/// Спільний валідний конфіг для тестів героїв.
///
/// Окремо, бо GameCatalog проганяє повний GameConfigValidator: щойно в конфігу
/// з'являється хоч один герой, він мусить мати клас із ростера, смуги вартості,
/// ставку джемів для свого рангу й будівлю призову. Тримати це в кожному
/// тестовому класі означало б чотири копії, які розійдуться на першій зміні.
/// </summary>
internal static class HeroTestConfig
{
    public const string Hall = "heroeshall";
    public const string Forge = "forge";
    public const string CommonHero = "warrior_bran";
    public const string UniqueHero = "mage_iselle";

    public const string Artifact = "necklace_dawn";
    public const string SecondArtifact = "ring_ember";

    public const int ShardPriceGold = 100;
    public const int SummonShards = 10;
    /// <summary>Номери слотів за типом — позиції в Equipment.ArtifactSlots.</summary>
    public const int NecklaceSlot = 0;
    public const int RingSlot = 2;

    public static GameConfig Create() => new()
    {
        Resources =
        [
            new ResourceConfig { Key = "gold" },
            new ResourceConfig { Key = "food" }
        ],
        Buildings =
        [
            new BuildingConfig { Key = "townhall", IsMainBuilding = true, UpgradeCostGrowth = 1.45 },
            new BuildingConfig
            {
                Key = Hall,
                UpgradeCostGrowth = 1.45,
                BaseBuildMinutes = 10,
                BuildTimeGrowth = 1.5,
                Cost = [new ResourceCost { Resource = "gold", Amount = 10 }]
            },
            new BuildingConfig { Key = "warehouse", StoresResources = ["gold", "food"], UpgradeCostGrowth = 1.45 },
            new BuildingConfig { Key = "hospital", UpgradeCostGrowth = 1.45 },
            new BuildingConfig { Key = Forge, UpgradeCostGrowth = 1.45 }
        ],
        Items =
        [
            .. TestKit.UniversalShards.All(),
            new ItemConfig { Key = "hero_essence_t2" },
            new ItemConfig { Key = "hero_essence_t3" },

            // Артефакти без базових статів: їхні стати випадкові й лежать
            // на екземплярі. Обидва з одного набору — на них перевіряється
            // і сет-бонус, і заборона дублікатів
            new ItemConfig
            {
                Key = Artifact,
                Type = "equipment",
                Slot = EquipmentSlot.Artifact,
                ArtifactSlot = "necklace",
                SetKey = "dawn"
            },
            new ItemConfig
            {
                Key = SecondArtifact,
                Type = "equipment",
                Slot = EquipmentSlot.Artifact,
                ArtifactSlot = "ring",
                SetKey = "dawn"
            }
        ],
        Equipment = new EquipmentConfig
        {
            ArtifactSlots =
            [
                new ArtifactSlotConfig { Key = "necklace", DisplayName = "Намисто" },
                new ArtifactSlotConfig { Key = "crown", DisplayName = "Корона" },
                new ArtifactSlotConfig { Key = "ring", DisplayName = "Кільце" },
                new ArtifactSlotConfig { Key = "belt", DisplayName = "Пояс" }
            ],
            MaxLevel = 20,
            LevelBonusPerLevel = 0.05,
            LevelExperienceBase = 40,
            LevelExperienceExponent = 1.4,
            FeedExperience = new Dictionary<Rarity, int> { [Rarity.Common] = 100, [Rarity.Rare] = 300 },
            MaxMastery = 10,
            MasteryBonusPerLevel = 0.1,
            ForgeBuildingKey = Forge,
            MasteryBaseGold = 500,
            MasteryCostGrowth = 1.7,
            SafeMasteryLevel = 3,
            SuccessDropPerLevel = 0.1,
            MinSuccessChance = 0.3,
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
            }
        },
        HeroSettings = new HeroesConfig
        {
            BuildingKey = Hall,
            HealBuildingKey = "hospital",
            MaxMarches = 8,
            StarPartCosts = [[1, 1, 2, 2, 2, 2], [5, 5, 5, 5, 5, 5], [10, 10, 10, 10, 10, 10], [20, 20, 20, 20, 20, 20], [40, 40, 40, 40, 40, 40]],
            UniversalShardUpgrade = new Dictionary<string, int> { ["Common"] = 100, ["Rare"] = 300 },
            HealCostPerLevel = [new ResourceCost { Resource = "food", Amount = 40 }],
            TierGrowth = 1.35,
            EvolutionItemKeys = ["hero_essence_t2", "hero_essence_t3"],
            Classes = ["warrior", "mage"]
        },
        Heroes =
        [
            new HeroConfig
            {
                Key = CommonHero,
                Class = "warrior",
                Rank = Rarity.Common,
                BaseStats = new Dictionary<string, double> { ["Attack"] = 40 },
                StatGrowth = new Dictionary<string, double> { ["Attack"] = 4 },
},
            new HeroConfig
            {
                Key = UniqueHero,
                Class = "mage",
                Rank = Rarity.Unique,
                BaseStats = new Dictionary<string, double> { ["Attack"] = 105 },
                StatGrowth = new Dictionary<string, double> { ["Attack"] = 13 },
}
        ]
    };

    public static GameCatalog Catalog() => new(Create());

    public static HeroProgression Progression() => new(Create().HeroSettings);
}
