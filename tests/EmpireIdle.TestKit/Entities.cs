using EmpireIdle.Domain.Entities;
using EmpireIdle.Domain.Enums;
using EmpireIdle.Domain.Services.Config;
using EmpireIdle.Domain.ValueObjects;

namespace EmpireIdle.TestKit;

/// <summary>Фабрики доменних сутностей для тестів.</summary>
public static class Entities
{
    /// <summary>
    /// Фіксований момент. Тести, що рахують час, мають виходити з одного
    /// нуля — інакше два прогони дають різні числа.
    /// </summary>
    public static readonly DateTime Now = new(2026, 6, 1, 12, 0, 0, DateTimeKind.Utc);

    // ---------- Село ----------

    /// <summary>
    /// Мінімальний конфіг для тестів села: ферма плюс ратуша.
    ///
    /// Ратуша потрібна не сама по собі: тірний гейт рахує від неї стелю
    /// рівномірності, і без неї будь-який апгрейд кидає.
    /// </summary>
    public static Dictionary<string, BuildingConfig> FarmConfigs(int baseCostFood = 100) => new()
    {
        [TestKeys.Townhall] = new BuildingConfig
        {
            Key = TestKeys.Townhall,
            IsMainBuilding = true,
            Cost = [new ResourceCost { Resource = TestKeys.Wood, Amount = 100 }],
            BaseBuildMinutes = 5,
            BuildTimeGrowth = 1.5,
            UpgradeCostGrowth = 1.45,
            RequiresMainBuildingLevel = 0
        },
        [TestKeys.Farm] = new BuildingConfig
        {
            Key = TestKeys.Farm,
            ProducesResource = TestKeys.Food,
            BaseProductionPerMinute = 10,
            Cost = [new ResourceCost { Resource = TestKeys.Food, Amount = baseCostFood }],
            BaseStorage = 60,
            BaseBuildMinutes = 5,
            BuildTimeGrowth = 1.5,
            UpgradeCostGrowth = 1.45,
            RequiresMainBuildingLevel = 0
        }
    };

    /// <summary>Порожнє село зі стандартними ресурсами.</summary>
    public static Village Village(Guid? playerId = null, int x = 0, int y = 0, int serverId = 1)
        => new(Guid.NewGuid(), playerId ?? Guid.NewGuid(), "Test Village",
            TestKeys.AllResources, x, y, serverId);

    /// <summary>Село з однаковою кількістю кожного ресурсу.</summary>
    public static Village VillageWithResources(int resourceAmount = 1000, Guid? playerId = null)
    {
        var village = Village(playerId);

        village.GrantStartingResources(
            TestKeys.AllResources.ToDictionary(r => r, _ => resourceAmount), Now);

        return village;
    }

    /// <summary>
    /// Село з ратушею потрібного рівня й фермою 1 рівня.
    ///
    /// Рівень ратуші за замовчуванням високий: правило C не пускає жодну
    /// будівлю вище за неї, тож із ратушею 1 рівня тести про вартість і час
    /// упирались би в гейт замість того, що перевіряють.
    /// </summary>
    public static Village VillageWithTownhall(int townhallLevel = 10, int resourceAmount = 100_000)
    {
        var village = VillageWithResources(resourceAmount);
        var configs = FarmConfigs();

        village.AddBuilding(TestKeys.Townhall, configs, Now);
        village.AddBuilding(TestKeys.Farm, configs, Now);

        RaiseLevel(village.Buildings.Single(b => b.Type == TestKeys.Townhall),
            configs[TestKeys.Townhall], townhallLevel - 1, Now);

        return village;
    }

    /// <summary>
    /// Піднімає рівень будівлі напряму, без вартості й часу.
    /// Тести про гейт не мають залежати від того, чи вистачило ресурсів.
    /// </summary>
    public static void RaiseLevel(Building building, BuildingConfig config, int times, DateTime utcNow)
    {
        for (var i = 0; i < times; i++)
        {
            building.BeginUpgrade(config, TimeSpan.Zero, utcNow, ProductionBoost.None, locationMultiplier: 1.0);
            building.CompleteConstruction(utcNow);
        }
    }

    // ---------- Герой і спорядження ----------

    /// <summary>Герой заданого рівня, тіру й сузір'я.</summary>
    public static Hero Hero(string heroKey = TestKeys.CommonHero, Guid? playerId = null,
        Guid? garrisonId = null, bool asLeader = true, int level = 1, int tier = 1,
        int constellation = 0, int maxConstellation = 6, int serverId = 1)
    {
        var hero = new Hero(Guid.NewGuid(), playerId ?? Guid.NewGuid(), serverId, heroKey,
            garrisonId ?? Guid.NewGuid(), asLeader, Now);

        for (var i = 1; i < level; i++)
            hero.GainLevel(level, Now);

        for (var i = 1; i < tier; i++)
            hero.EvolveTier(tier, Now);

        for (var i = 0; i < constellation; i++)
            hero.TryAddConstellation(maxConstellation, Now);

        return hero;
    }

    /// <summary>Екземпляр спорядження із заданими статами.</summary>
    public static EquipmentItem Equipment(string itemKey, EquipmentSlot slot, Guid? playerId = null,
        Rarity rarity = Rarity.Common, params (string Stat, double Value)[] stats)
        => new(Guid.NewGuid(), playerId ?? Guid.NewGuid(), 1, itemKey, slot, rarity,
            stats.Length > 0 ? stats : [("Attack", 10.0)], Now);
}
