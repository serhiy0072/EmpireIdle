using EmpireIdle.Application.Interfaces;
using EmpireIdle.Application.Inventory.Contracts;
using EmpireIdle.Application.Inventory.Effects;
using EmpireIdle.Domain.Entities;
using EmpireIdle.Domain.Services;
using EmpireIdle.Domain.Services.Config;
using EmpireIdle.TestKit;
using NSubstitute;

namespace EmpireIdle.Application.Tests.Inventory;

/// <summary>Банка енергії (рішення 11.10.2026): додає енергію данжів і понад повну шкалу.</summary>
public class DungeonEnergyItemEffectTests
{
    private static readonly DateTime Now = new(2026, 10, 11, 12, 0, 0, DateTimeKind.Utc);
    private static readonly ItemConfig Jar = new() { Key = "dungeon_energy_10", Type = "energy", DungeonEnergy = 10 };

    private readonly Guid _playerId = Guid.NewGuid();
    private readonly IDungeonRepository _dungeons = Substitute.For<IDungeonRepository>();
    private readonly GameCatalog _catalog = new GameConfigBuilder().WithDungeons().BuildCatalog();

    private Task Drink(int count)
        => new DungeonEnergyItemEffect(_dungeons, _catalog)
            .ApplyAsync(new ItemUsageContext(_playerId, Jar, count, Now), CancellationToken.None);

    [Fact]
    public async Task Apply_ShouldAddEveryJar_OnTopOfAFullGauge()
    {
        var max = _catalog.Config.Dungeons.MaxEnergy;
        var energy = new DungeonEnergy(Guid.NewGuid(), _playerId, max, Now);
        _dungeons.GetEnergyAsync(_playerId, Arg.Any<CancellationToken>()).Returns(energy);

        await Drink(3);

        Assert.Equal(max + 30, energy.Current(max, _catalog.Config.Dungeons.RegenHours, Now));
    }

    /// <summary>Хто ще не був у данжі, має повну шкалу — банка лягає поверх неї.</summary>
    [Fact]
    public async Task Apply_ShouldStartFromAFullGauge_ForANewcomer()
    {
        DungeonEnergy? added = null;
        await _dungeons.AddEnergyAsync(Arg.Do<DungeonEnergy>(e => added = e), Arg.Any<CancellationToken>());

        await Drink(1);

        var max = _catalog.Config.Dungeons.MaxEnergy;
        Assert.Equal(max + 10, added!.Current(max, _catalog.Config.Dungeons.RegenHours, Now));
    }
}
