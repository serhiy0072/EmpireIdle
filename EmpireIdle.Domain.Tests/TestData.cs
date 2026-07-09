using EmpireIdle.Domain.Entities;

namespace EmpireIdle.Domain.Tests;

/// <summary>
/// Фабрики для створення доменних об'єктів у тестах.
/// Тримає рутину конструювання в одному місці, щоб тести лишались читабельними.
/// </summary>
internal static class TestData
{
    /// <summary>Стандартний набір ресурсів для тестового села.</summary>
    public static readonly string[] DefaultResources = { "gold", "food", "wood" };

    /// <summary>Створює порожнє село зі стандартними ресурсами (по нулю кожного).</summary>
    public static Village CreateVillage(Guid? playerId = null)
        => new(Guid.NewGuid(), playerId ?? Guid.NewGuid(), "Test Village", DefaultResources);
}