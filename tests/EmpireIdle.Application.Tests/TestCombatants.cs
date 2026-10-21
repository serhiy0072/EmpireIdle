using EmpireIdle.Application.Interfaces;
using EmpireIdle.Application.Marches.Services;
using EmpireIdle.Domain.Entities;
using EmpireIdle.Domain.Services;
using NSubstitute;

namespace EmpireIdle.Application.Tests;

/// <summary>
/// Герої-бійці й захисники для тестів, яким спорядження не потрібне: інвентар порожній,
/// тож бонуси військам дають лише вміння, а сила героя — власні стати.
/// </summary>
internal static class TestCombatants
{
    public static HeroCombatants WithoutGear(GameCatalog catalog)
        => new(EmptyInventory(), Stats(catalog), catalog);

    /// <summary>Захисники села з героїв цього репозиторію — без спорядження, за власною силою.</summary>
    public static GarrisonDefenders Defenders(IHeroRepository heroes, GameCatalog catalog)
        => new(heroes, EmptyInventory(), Stats(catalog), catalog);

    private static HeroStats Stats(GameCatalog catalog) => new(new HeroProgression(catalog.Config.HeroSettings), catalog);

    private static IInventoryRepository EmptyInventory()
    {
        var inventory = Substitute.For<IInventoryRepository>();
        inventory.GetEquippedAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>()).Returns(new List<EquipmentItem>());
        inventory.GetEquippedByHeroesAsync(Arg.Any<IReadOnlyCollection<Guid>>(), Arg.Any<CancellationToken>()).Returns(new List<EquipmentItem>());

        return inventory;
    }
}
