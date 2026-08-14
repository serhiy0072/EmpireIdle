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

    /// <summary>Стандартний набір зон тестового села.</summary>
    public static readonly (string Type, int Slots)[] DefaultZones =
        { ("plain", 4), ("forest", 3), ("mountain", 3), ("water", 2) };

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

    /// <summary>Створює село зі стандартними ресурсами й зонами, ініціалізованими з вказаною кількістю ресурсів.</summary>
    public static Village CreateVillageWithResources(int resourceAmount = 0, Guid? playerId = null)
    {
        var village = CreateVillage(playerId);

        // Manually update resource amounts using reflection
        var resourcesField = village.GetType().GetField("_resources",
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);

        if (resourcesField?.GetValue(village) is System.Collections.Generic.List<VillageResource> resourcesList)
        {
            foreach (var resource in resourcesList)
            {
                resource.Add(resourceAmount);
            }
        }

        return village;
    }
}
