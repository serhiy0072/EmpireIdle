using EmpireIdle.Domain.Enums;
using EmpireIdle.Domain.Services.Config;

namespace EmpireIdle.TestKit;

/// <summary>
/// Універсальні осколки трьох рідкостей (GDD §6.1). Валідатор вимагає їх для кожної рідкості ростеру,
/// тож будь-який тестовий конфіг із героями мусить їх мати.
/// </summary>
public static class UniversalShards
{
    public const string Common = "universal_shard_common";
    public const string Rare = "universal_shard_rare";
    public const string Unique = "universal_shard_unique";

    public static ItemConfig[] All() =>
    [
        Item(Common, Rarity.Common),
        Item(Rare, Rarity.Rare),
        Item(Unique, Rarity.Unique)
    ];

    private static ItemConfig Item(string key, Rarity rarity) => new()
    {
        Key = key,
        DisplayName = key,
        Description = "",
        Rarity = rarity,
        Type = "universalshard"
    };
}
