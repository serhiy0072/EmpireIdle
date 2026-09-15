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

    public const string WarriorWeapon = "sword_iron";
    public const string BetterWarriorWeapon = "sword_steel";
    public const string Artifact = "amulet_dawn";
    public const string SecondArtifact = "ring_ember";

    public const int ShardPriceGold = 100;
    public const int SummonShards = 10;
    public const int LevelUpGold = 100;
    public const int WeaponPriceGold = 500;
    public const int ArtifactSlots = 4;

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
            new ItemConfig { Key = "hero_essence_t2" },
            new ItemConfig { Key = "hero_essence_t3" },

            // Зброя воїна: клас звужений навмисно, на ньому тримається
            // перевірка придатності
            new ItemConfig
            {
                Key = WarriorWeapon,
                Type = "equipment",
                Slot = EquipmentSlot.Weapon,
                WeaponClasses = ["warrior"],
                BaseStats = new Dictionary<string, double> { ["Attack"] = 10 },
                PriceGold = WeaponPriceGold
            },
            new ItemConfig
            {
                Key = BetterWarriorWeapon,
                Type = "equipment",
                Slot = EquipmentSlot.Weapon,
                WeaponClasses = ["warrior"],
                BaseStats = new Dictionary<string, double> { ["Attack"] = 18 },
                PriceGold = WeaponPriceGold * 2
            },

            // Артефакти без базових статів: їхні стати випадкові й лежать
            // на екземплярі. Обидва з одного набору — на них перевіряється
            // і сет-бонус, і заборона дублікатів
            new ItemConfig
            {
                Key = Artifact,
                Type = "equipment",
                Slot = EquipmentSlot.Artifact,
                SetKey = "dawn"
            },
            new ItemConfig
            {
                Key = SecondArtifact,
                Type = "equipment",
                Slot = EquipmentSlot.Artifact,
                SetKey = "dawn"
            }
        ],
        Equipment = new EquipmentConfig
        {
            ArtifactSlots = ArtifactSlots,
            MaxEnhancement = 20,
            EnhancementBonusPerLevel = 0.1,
            ForgeBuildingKey = Forge
        },
        HeroSettings = new HeroesConfig
        {
            BuildingKey = Hall,
            HealBuildingKey = "hospital",
            LevelsPerTier = 10,
            MaxTier = 3,
            MaxMarches = 8,
            MaxConstellation = 6,
            BaseLevelUpMinutes = 4,
            HealCostPerLevel = [new ResourceCost { Resource = "food", Amount = 40 }],
            TierStatMultipliers = [1.0, 1.35, 1.8],
            EvolutionItemKeys = ["hero_essence_t2", "hero_essence_t3"],
            OverflowGems = new Dictionary<string, int> { ["Common"] = 0, ["Rare"] = 15, ["Unique"] = 40 },
            Classes = ["warrior", "mage"]
        },
        Heroes =
        [
            new HeroConfig
            {
                Key = CommonHero,
                Class = "warrior",
                Rank = Rarity.Common,
                SummonShards = SummonShards,
                ShardPriceGold = ShardPriceGold,
                BaseStats = new Dictionary<string, double> { ["Attack"] = 40 },
                StatGrowth = new Dictionary<string, double> { ["Attack"] = 4 },
                LevelUpCosts = [Band()]
            },
            new HeroConfig
            {
                Key = UniqueHero,
                Class = "mage",
                Rank = Rarity.Unique,
                BaseStats = new Dictionary<string, double> { ["Attack"] = 105 },
                StatGrowth = new Dictionary<string, double> { ["Attack"] = 13 },
                LevelUpCosts = [Band()]
            }
        ]
    };

    public static GameCatalog Catalog() => new(Create());

    public static HeroProgression Progression() => new(Create().HeroSettings);

    private static HeroLevelCostBand Band() => new()
    {
        FromLevel = 1,
        Cost = [new ResourceCost { Resource = "gold", Amount = LevelUpGold }]
    };
}
