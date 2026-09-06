using EmpireIdle.Domain.Entities;
using EmpireIdle.Domain.Services.Config;
using EmpireIdle.Domain.ValueObjects;

namespace EmpireIdle.Domain.Tests;

/// <summary>
/// Фабрики для створення доменних об'єктів у тестах.
/// </summary>
internal static class TestData
{
    /// <summary>Стандартний набір ресурсів тестового села.</summary>
    public static readonly string[] DefaultResources = { "gold", "food", "wood", "iron" };

    /// <summary>
    /// Мінімальний конфіг для тестів села: ферма плюс ратуша.
    ///
    /// Ратуша потрібна не сама по собі: тірний гейт рахує від неї стелю
    /// рівномірності, і без неї будь-який апгрейд кидає.
    /// </summary>
    public static Dictionary<string, BuildingConfig> FarmConfigs(int baseCostFood = 100) => new()
    {
        ["townhall"] = new BuildingConfig
        {
            Key = "townhall",
            IsMainBuilding = true,
            Cost = new List<ResourceCost> { new() { Resource = "wood", Amount = 100 } },
            BaseBuildMinutes = 5,
            BuildTimeGrowth = 1.5,
            UpgradeCostGrowth = 1.45,
            RequiresMainBuildingLevel = 0
        },
        ["farm"] = new BuildingConfig
        {
            Key = "farm",
            ProducesResource = "food",
            BaseProductionPerMinute = 10,
            Cost = new List<ResourceCost> { new() { Resource = "food", Amount = baseCostFood } },
            BaseStorage = 60,
            BaseBuildMinutes = 5,
            BuildTimeGrowth = 1.5,
            UpgradeCostGrowth = 1.45,
            RequiresMainBuildingLevel = 0
        }
    };

    /// <summary>Створює порожнє село зі стандартними ресурсами.</summary>
    public static Village CreateVillage(Guid? playerId = null, int x = 0, int y = 0)
        => new(Guid.NewGuid(), playerId ?? Guid.NewGuid(), "Test Village", DefaultResources, x, y);

    /// <summary>Створює село з однаковою кількістю кожного ресурсу.</summary>
    public static Village CreateVillageWithResources(int resourceAmount = 1000, Guid? playerId = null)
    {
        var village = CreateVillage(playerId);
        village.GrantStartingResources(DefaultResources.ToDictionary(r => r, _ => resourceAmount), DateTime.UtcNow);
        return village;
    }

    /// <summary>
    /// Село з ратушею потрібного рівня й фермою 1 рівня.
    ///
    /// Рівень ратуші за замовчуванням високий: правило C не пускає жодну
    /// будівлю вище за неї, тож із ратушею 1 рівня тести про вартість і час
    /// упирались би в гейт замість того, що перевіряють.
    /// </summary>
    public static Village CreateVillageWithTownhall(int townhallLevel = 10, int resourceAmount = 100_000)
    {
        var village = CreateVillageWithResources(resourceAmount);
        var configs = FarmConfigs();
        var now = DateTime.UtcNow;

        village.AddBuilding("townhall", configs, now);
        village.AddBuilding("farm", configs, now);

        RaiseLevel(village.Buildings.Single(b => b.Type == "townhall"), configs["townhall"], townhallLevel - 1, now);

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
}
