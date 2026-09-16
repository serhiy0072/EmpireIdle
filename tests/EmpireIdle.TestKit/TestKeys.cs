namespace EmpireIdle.TestKit;

/// <summary>
/// Ключі, якими користуються фікстури.
///
/// Константи, а не рядки на місці: перейменування в конфігу тоді ламає
/// компіляцію, а не тест у рантаймі з повідомленням «ключ не знайдено».
/// </summary>
public static class TestKeys
{
    public const string Gold = "gold";
    public const string Food = "food";
    public const string Wood = "wood";
    public const string Iron = "iron";

    public static readonly string[] AllResources = [Gold, Food, Wood, Iron];

    public const string Townhall = "townhall";
    public const string Farm = "farm";
    public const string Warehouse = "warehouse";
    public const string Barracks = "barracks";
    public const string Hall = "heroeshall";
    public const string Hospital = "hospital";
    public const string Forge = "forge";

    public const string Infantry = "infantry";
    public const string Archer = "archer";
    public const string Cavalry = "cavalry";

    public const string CommonHero = "warrior_bran";
    public const string UniqueHero = "mage_iselle";
    public const string PlainHero = "plain_hero";

    public const string EssenceT2 = "hero_essence_t2";
    public const string EssenceT3 = "hero_essence_t3";

    public const string Weapon = "sword_iron";
    public const string BetterWeapon = "sword_steel";
    public const string Artifact = "dawn_amulet";
    public const string SecondArtifact = "dawn_ring";
    public const string ThirdArtifact = "dawn_sigil";
    public const string FourthArtifact = "dawn_chime";
    public const string LooseArtifact = "lone_charm";
    public const string SetKey = "dawn";

    public const string Terrain = "plain";
}
