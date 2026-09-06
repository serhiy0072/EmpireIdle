using AwesomeAssertions;
using EmpireIdle.Application.Clans.Services;
using EmpireIdle.Application.Interfaces;
using EmpireIdle.Domain.Entities;
using EmpireIdle.Domain.Enums;
using EmpireIdle.Domain.Services;
using EmpireIdle.Domain.Services.Config;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;

namespace EmpireIdle.Application.Tests.Clans;

/// <summary>
/// Повернення підкріплень додому. Ключове — юніти не телепортуються:
/// зняті з чужого гарнізону, вони мусять з'явитись у марші й ніде більше,
/// інакше армія або подвоюється, або зникає.
/// </summary>
public class ReinforcementReturnerTests
{
    private static readonly DateTime Now = new(2026, 6, 1, 12, 0, 0, DateTimeKind.Utc);
    private static readonly Guid OwnerId = Guid.NewGuid();
    private static readonly Guid HostId = Guid.NewGuid();

    private readonly IGarrisonRepository _garrisons = Substitute.For<IGarrisonRepository>();
    private readonly IVillageRepository _villages = Substitute.For<IVillageRepository>();
    private readonly IMarchRepository _marches = Substitute.For<IMarchRepository>();

    private static GameConfig Config() => new()
    {
        Buildings = [new BuildingConfig { Key = "townhall", IsMainBuilding = true }],
        Units = [new UnitConfig { Key = "infantry", Stats = new Dictionary<string, double> { ["Speed"] = 4 } }],
        Map = new MapConfig
        {
            Width = 100,
            Height = 100,
            TerrainSeed = 1,
            Terrains = [new TerrainConfig { Type = "plain", Weight = 1, Passable = true, MoveCost = 1.0, Habitable = true }]
        }
    };

    private ReinforcementReturner Returner()
    {
        var config = Config();
        var catalog = new GameCatalog(config);

        return new ReinforcementReturner(
            _garrisons, _villages, _marches,
            new MarchCalculator(new TerrainGenerator(config.Map), catalog),
            NullLogger<ReinforcementReturner>.Instance);
    }

    /// <summary>Село господаря з гарнізоном, у якому стоять війська власника.</summary>
    private (Garrison Host, Garrison Owner) Deployed(int infantry = 10)
    {
        var hostVillage = new Village(Guid.NewGuid(), HostId, "Host", ["food"], 50, 50);
        var ownerVillage = new Village(Guid.NewGuid(), OwnerId, "Owner", ["food"], 60, 50);

        var host = new Garrison(Guid.NewGuid(), hostVillage.Id, 1);
        var owner = new Garrison(Guid.NewGuid(), ownerVillage.Id, 1);

        host.AddReinforcements(OwnerId, owner.Id,
            new Dictionary<string, int> { ["infantry"] = infantry }, 100, Now.AddHours(-3));

        _garrisons.GetHoldingReinforcementsAsync(OwnerId, Arg.Any<CancellationToken>()).Returns([host]);
        _garrisons.GetByVillageIdAsync(hostVillage.Id, Arg.Any<CancellationToken>()).Returns(host);
        _garrisons.GetByIdAsync(owner.Id, Arg.Any<CancellationToken>()).Returns(owner);
        _villages.GetByIdAsync(hostVillage.Id, Arg.Any<CancellationToken>()).Returns(hostVillage);
        _villages.GetByIdAsync(ownerVillage.Id, Arg.Any<CancellationToken>()).Returns(ownerVillage);

        return (host, owner);
    }

    [Fact]
    public async Task ReturnAllOfPlayer_sends_the_units_home_as_a_march()
    {
        var (host, owner) = Deployed(infantry: 10);

        var sent = await Returner().ReturnAllOfPlayerAsync(OwnerId, Now);

        sent.Should().Be(1);
        host.ReinforcementCount.Should().Be(0);

        // Юніти в дорозі, а не в гарнізоні власника: доступними стануть на прибутті
        owner.Units.Should().BeEmpty();

        await _marches.Received(1).AddAsync(
            Arg.Is<March>(m => m.GarrisonId == owner.Id
                            && m.State == MarchState.Returning
                            && m.Intent == MarchIntent.Reinforce
                            && m.GetUnits()["infantry"] == 10
                            && m.ArrivesAt > Now),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ReturnAllOfPlayer_does_nothing_when_the_player_has_no_troops_abroad()
    {
        _garrisons.GetHoldingReinforcementsAsync(OwnerId, Arg.Any<CancellationToken>()).Returns([]);

        var sent = await Returner().ReturnAllOfPlayerAsync(OwnerId, Now);

        sent.Should().Be(0);

        await _marches.DidNotReceiveWithAnyArgs().AddAsync(default!, default);
    }

    [Fact]
    public async Task ReturnAllFromVillage_evicts_every_guest()
    {
        var hostVillage = new Village(Guid.NewGuid(), HostId, "Host", ["food"], 50, 50);
        var host = new Garrison(Guid.NewGuid(), hostVillage.Id, 1);

        var firstOwner = Guid.NewGuid();
        var secondOwner = Guid.NewGuid();

        foreach (var (ownerId, count) in new[] { (firstOwner, 8), (secondOwner, 4) })
        {
            var ownerVillage = new Village(Guid.NewGuid(), ownerId, "Ally", ["food"], 55, 50);
            var ownerGarrison = new Garrison(Guid.NewGuid(), ownerVillage.Id, 1);

            host.AddReinforcements(ownerId, ownerGarrison.Id,
                new Dictionary<string, int> { ["infantry"] = count }, 100, Now.AddHours(-2));

            _garrisons.GetByIdAsync(ownerGarrison.Id, Arg.Any<CancellationToken>()).Returns(ownerGarrison);
            _villages.GetByIdAsync(ownerVillage.Id, Arg.Any<CancellationToken>()).Returns(ownerVillage);
        }

        _garrisons.GetByVillageIdAsync(hostVillage.Id, Arg.Any<CancellationToken>()).Returns(host);
        _villages.GetByIdAsync(hostVillage.Id, Arg.Any<CancellationToken>()).Returns(hostVillage);

        var sent = await Returner().ReturnAllFromVillageAsync(hostVillage.Id, Now);

        sent.Should().Be(2);
        host.ReinforcementCount.Should().Be(0);

        await _marches.ReceivedWithAnyArgs(2).AddAsync(default!, default);
    }

    [Fact]
    public async Task Troops_are_dropped_when_the_home_village_is_gone()
    {
        var (host, owner) = Deployed(infantry: 10);

        // Село власника зникло, поки війська стояли в гостях
        _villages.GetByIdAsync(owner.VillageId, Arg.Any<CancellationToken>()).Returns((Village?)null);

        var sent = await Returner().ReturnAllOfPlayerAsync(OwnerId, Now);

        // Повертати нікуди, але й лишати чужі війська в гарнізоні не можна
        sent.Should().Be(0);
        host.ReinforcementCount.Should().Be(0);

        await _marches.DidNotReceiveWithAnyArgs().AddAsync(default!, default);
    }
}
