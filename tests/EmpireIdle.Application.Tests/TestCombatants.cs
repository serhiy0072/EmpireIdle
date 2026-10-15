using EmpireIdle.Application.Interfaces;
using EmpireIdle.Application.Marches.Services;
using EmpireIdle.Domain.Entities;
using EmpireIdle.Domain.Services;
using NSubstitute;

namespace EmpireIdle.Application.Tests;

/// <summary>
/// Герої-бійці для тестів, яким спорядження не потрібне: інвентар порожній,
/// тож бонуси військам дають лише вміння, а сила героя — власні стати.
/// </summary>
internal static class TestCombatants
{
    public static HeroCombatants WithoutGear(GameCatalog catalog)
    {
        var inventory = Substitute.For<IInventoryRepository>();
        inventory.GetEquippedAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>()).Returns(new List<EquipmentItem>());

        return new HeroCombatants(inventory, new HeroStats(new HeroProgression(catalog.Config.HeroSettings), catalog), catalog);
    }
}
