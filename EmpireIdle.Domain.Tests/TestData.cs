using EmpireIdle.Domain.Entities;
using EmpireIdle.Domain.Services;

namespace EmpireIdle.Domain.Tests;

/// <summary>
/// Фабрики для створення доменних об'єктів у тестах.
/// </summary>
internal static class TestData
{
    /// <summary>Стандартний набір ресурсів тестового села.</summary>
    public static readonly string[] DefaultResources = { "gold", "food", "wood", "iron" };

    /// <summary>Мінімальний конфіг ферми для тестів (зона plain, без порога Ратуші).</summary>
    public static Dictionary<string, BuildingConfig> FarmConfigs(int baseCostFood = 100) => new()
    {
        ["farm"] = new BuildingConfig
        {
            Key = "farm",
            ProducesResource = "food",
            BaseProductionPerMinute = 10,
            Cost = new List<ResourceCost> { new() { Resource = "food", Amount = baseCostFood } },
            BaseStorage = 60,
            StorageGrowth = 1.3,
            BaseBuildMinutes = 5,
            BuildTimeGrowth = 1.5,
            RequiresMainBuildingLevel = 0
        }
    };

    /// <summary>Створює порожнє село зі стандартними ресурсами й зонами.</summary>
    public static Village CreateVillage(Guid? playerId = null, int x = 0, int y = 0)
        => new(Guid.NewGuid(), playerId ?? Guid.NewGuid(), "Test Village", DefaultResources, x, y);

    /// <summary>Створює село з однаковою кількістю кожного ресурсу.</summary>
    public static Village CreateVillageWithResources(int resourceAmount = 1000, Guid? playerId = null)
    {
        var village = CreateVillage(playerId);
        village.GrantStartingResources(DefaultResources.ToDictionary(r => r, _ => resourceAmount));
        return village;
    }
}
