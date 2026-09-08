using AwesomeAssertions;
using EmpireIdle.Application.Interfaces;
using EmpireIdle.Application.Marches.Services;
using EmpireIdle.Domain.Entities;
using EmpireIdle.Domain.Exceptions;
using EmpireIdle.Domain.Services;
using EmpireIdle.Domain.Services.Config;
using EmpireIdle.Domain.ValueObjects;
using NSubstitute;

namespace EmpireIdle.Application.Tests.Marches;

/// <summary>
/// Правила підкріплень перевіряються двічі — при відправленні й на прибутті.
/// Тут вони тестуються прямо, без маршів і хендлерів.
/// </summary>
public class ReinforcementRulesTests
{
    private static readonly DateTime Now = new(2026, 6, 1, 12, 0, 0, DateTimeKind.Utc);
    private static readonly Guid OwnerId = Guid.NewGuid();
    private static readonly Guid HostId = Guid.NewGuid();
    private static readonly Guid ClanId = Guid.NewGuid();

    private readonly IClanRepository _clans = Substitute.For<IClanRepository>();
    private readonly IGarrisonRepository _garrisons = Substitute.For<IGarrisonRepository>();

    private static GameConfig Config() => new()
    {
        Buildings =
        [
            new BuildingConfig { Key = "townhall", IsMainBuilding = true, UpgradeCostGrowth = 1.45 },
            new BuildingConfig { Key = "embassy", ReinforcementSlotsPerLevel = 50, UpgradeCostGrowth = 1.45 }
        ],
        Combat = new CombatConfig { NewbieShieldTownHallLevel = 3 }
    };

    private ReinforcementRules Rules()
    {
        var catalog = new GameCatalog(Config());

        return new ReinforcementRules(_clans, _garrisons, catalog,
            new VillageStatus(catalog), new VillageCapacities(catalog));
    }

    /// <summary>Село з ратушею потрібного рівня й посольством 1 рівня.</summary>
    private static Village NewVillage(Guid ownerId, int townHallLevel = 5)
    {
        var catalog = new GameCatalog(Config());
        var village = new Village(Guid.NewGuid(), ownerId, "Test", ["food"], 50, 50);

        village.AddBuilding("townhall", catalog.Buildings, Now);
        village.AddBuilding("embassy", catalog.Buildings, Now);

        var townhall = village.Buildings.Single(b => b.Type == "townhall");

        for (var i = 1; i < townHallLevel; i++)
        {
            townhall.BeginUpgrade(catalog.Buildings["townhall"], TimeSpan.Zero, Now, ProductionBoost.None, 1.0);
            townhall.CompleteConstruction(Now);
        }

        return village;
    }

    /// <summary>Ціль-село з гарнізоном; за замовчуванням порожнім.</summary>
    private MarchTarget GivenTarget(Village village, int alreadyHosted = 0)
    {
        var garrison = new Garrison(Guid.NewGuid(), village.Id, 1);

        if (alreadyHosted > 0)
            garrison.AddReinforcements(Guid.NewGuid(), Guid.NewGuid(),
                new Dictionary<string, int> { ["infantry"] = alreadyHosted }, 1000, Now);

        _garrisons.GetByVillageIdAsync(village.Id, Arg.Any<CancellationToken>()).Returns(garrison);

        return new MarchTarget(village.X, village.Y, village.Name, 5, village, [], 1.0);
    }

    private void GivenClans(Guid? ownerClan, Guid? hostClan)
    {
        _clans.GetClanIdByMemberAsync(OwnerId, Arg.Any<CancellationToken>()).Returns(ownerClan);
        _clans.GetClanIdByMemberAsync(HostId, Arg.Any<CancellationToken>()).Returns(hostClan);
    }

    [Fact]
    public async Task Clanmates_with_room_are_allowed()
    {
        var origin = NewVillage(OwnerId);
        var target = GivenTarget(NewVillage(HostId));

        GivenClans(ClanId, ClanId);

        var act = () => Rules().EnsureAllowedAsync(origin, target, incomingUnits: 10, default);

        await act.Should().NotThrowAsync();
    }

    [Fact]
    public async Task Own_village_is_rejected()
    {
        var origin = NewVillage(OwnerId);
        var target = GivenTarget(NewVillage(OwnerId));

        var act = () => Rules().EnsureAllowedAsync(origin, target, 10, default);

        await act.Should().ThrowAsync<RequirementNotMetException>();
    }

    [Fact]
    public async Task Sender_under_the_shield_is_rejected()
    {
        // Ратуша 1 — щит іще діє, підкріплення закриті
        var origin = NewVillage(OwnerId, townHallLevel: 1);
        var target = GivenTarget(NewVillage(HostId));

        GivenClans(ClanId, ClanId);

        var act = () => Rules().EnsureAllowedAsync(origin, target, 10, default);

        await act.Should().ThrowAsync<RequirementNotMetException>();
    }

    [Fact]
    public async Task Shielded_village_cannot_receive()
    {
        var origin = NewVillage(OwnerId);
        var target = GivenTarget(NewVillage(HostId, townHallLevel: 1));

        GivenClans(ClanId, ClanId);

        var act = () => Rules().EnsureAllowedAsync(origin, target, 10, default);

        await act.Should().ThrowAsync<RequirementNotMetException>();
    }

    [Fact]
    public async Task Strangers_are_rejected()
    {
        var origin = NewVillage(OwnerId);
        var target = GivenTarget(NewVillage(HostId));

        GivenClans(ClanId, Guid.NewGuid());

        var act = () => Rules().EnsureAllowedAsync(origin, target, 10, default);

        await act.Should().ThrowAsync<RequirementNotMetException>();
    }

    [Fact]
    public async Task Player_without_a_clan_is_rejected()
    {
        var origin = NewVillage(OwnerId);
        var target = GivenTarget(NewVillage(HostId));

        GivenClans(null, ClanId);

        var act = () => Rules().EnsureAllowedAsync(origin, target, 10, default);

        await act.Should().ThrowAsync<RequirementNotMetException>();
    }

    [Fact]
    public async Task Full_embassy_is_rejected()
    {
        var origin = NewVillage(OwnerId);

        // Посольство 1 рівня вміщає 50, з них 45 уже зайнято
        var target = GivenTarget(NewVillage(HostId), alreadyHosted: 45);

        GivenClans(ClanId, ClanId);

        var act = () => Rules().EnsureAllowedAsync(origin, target, incomingUnits: 10, default);

        await act.Should().ThrowAsync<RequirementNotMetException>();
    }

    /// <summary>
    /// На прибутті відмова — це розворот, а не помилка: прогін сканера
    /// не має падати через те, що союзник вийшов із клану.
    /// </summary>
    [Fact]
    public async Task Arrival_check_returns_a_reason_instead_of_throwing()
    {
        var origin = NewVillage(OwnerId);
        var destination = NewVillage(HostId);

        GivenTarget(destination);
        GivenClans(ClanId, Guid.NewGuid());

        var refusal = await Rules().CheckOnArrivalAsync(origin, destination, 10, default);

        refusal.Should().NotBeNull();
    }

    [Fact]
    public async Task Arrival_check_passes_for_clanmates_with_room()
    {
        var origin = NewVillage(OwnerId);
        var destination = NewVillage(HostId);

        GivenTarget(destination);
        GivenClans(ClanId, ClanId);

        var refusal = await Rules().CheckOnArrivalAsync(origin, destination, 10, default);

        refusal.Should().BeNull();
    }
}
