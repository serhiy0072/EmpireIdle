using EmpireIdle.Application.Interfaces;
using EmpireIdle.Application.Marches.Services;
using EmpireIdle.Domain.Combat;
using EmpireIdle.Domain.Entities;
using EmpireIdle.Domain.Enums;
using EmpireIdle.Domain.Exceptions;
using EmpireIdle.Domain.Services;
using EmpireIdle.Domain.Services.Config;
using EmpireIdle.Domain.ValueObjects;
using NSubstitute;

namespace EmpireIdle.Application.Tests.Marches;

/// <summary>
/// Табір як ціль (§2.5): його видно й атакують, як село, але стін у полі немає,
/// а свій табір не атакують.
/// </summary>
public class CampTargetTests
{
    private static readonly DateTime Now = new(2026, 9, 26, 12, 0, 0, DateTimeKind.Utc);

    private readonly IVillageRepository _villages = Substitute.For<IVillageRepository>();
    private readonly IGarrisonRepository _garrisons = Substitute.For<IGarrisonRepository>();
    private readonly IMarchRepository _marches = Substitute.For<IMarchRepository>();

    private static GameConfig Config() => new()
    {
        Buildings =
        [
            new BuildingConfig { Key = "townhall", IsMainBuilding = true, UpgradeCostGrowth = 1.45 },
            new BuildingConfig { Key = "wall", DefenceBonusPerLevel = 0.1, UpgradeCostGrowth = 1.45 }
        ],
        Combat = new CombatConfig { NewbieShieldTownHallLevel = 3 }
    };

    private MarchTargetResolver Resolver()
    {
        var catalog = new GameCatalog(Config());

        return new MarchTargetResolver(
            Substitute.For<IMonsterRepository>(), _villages, _garrisons, Substitute.For<IHeroRepository>(),
            new MonsterArmyBuilder(catalog), new HeroCombatModifiers(catalog), catalog, new VillageStatus(catalog),
            Substitute.For<IClanStructureRepository>(), Substitute.For<IClanRepository>(),
            new ClanTerritoryRules(catalog), _marches, TestEffects.Resolver(Substitute.For<IActiveEffectRepository>()));
    }

    /// <summary>Село з ратушею 5 і стіною 3 — вдома воно б'ється з бонусом +30%.</summary>
    private static Village NewVillage(string name, int x = 10, int y = 10)
    {
        var catalog = new GameCatalog(Config());
        var village = new Village(Guid.NewGuid(), Guid.NewGuid(), name, ["food"], x, y);

        village.AddBuilding("townhall", catalog.Buildings, Now);
        village.AddBuilding("wall", catalog.Buildings, Now);

        foreach (var (type, level) in new[] { ("townhall", 5), ("wall", 3) })
        {
            var building = village.Buildings.Single(b => b.Type == type);

            while (building.Level.Value < level)
            {
                building.BeginUpgrade(catalog.Buildings[type], TimeSpan.Zero, Now, ProductionBoost.None, 1.0);
                building.CompleteConstruction(Now);
            }
        }

        return village;
    }

    /// <summary>Армія власника <paramref name="owner"/> стоїть табором на (40, 40).</summary>
    private March GivenCamp(Village owner, bool camping = true)
    {
        var garrison = new Garrison(Guid.NewGuid(), owner.Id, 1);

        var camp = new March(Guid.NewGuid(), 1, garrison.Id, heroId: null, owner.X, owner.Y, 40, 40,
            MarchTargetType.Village, Guid.NewGuid(),
            new Dictionary<UnitStackKey, int> { [new UnitStackKey("infantry", 2)] = 30 },
            Now.AddHours(-1), Now.AddHours(-2));

        if (camping)
            camp.Camp(Now.AddHours(-1));

        _marches.GetByIdAsync(camp.Id, Arg.Any<CancellationToken>()).Returns(camp);
        _garrisons.GetByIdAsync(garrison.Id, Arg.Any<CancellationToken>()).Returns(garrison);
        _villages.GetByIdAsync(owner.Id, Arg.Any<CancellationToken>()).Returns(owner);

        return camp;
    }

    [Fact]
    public async Task ResolveAsync_ShouldDescribeTheCamp_WithoutTheOwnersWalls()
    {
        var owner = NewVillage("Табірне");
        var camp = GivenCamp(owner);

        var target = await Resolver().ResolveAsync(MarchTargetType.Camp, camp.Id, NewVillage("Нападник"), Now,
            CancellationToken.None);

        Assert.Equal((40, 40), (target.X, target.Y));
        Assert.Equal("Табірне", target.Name);
        Assert.Same(camp, target.Camp);
        Assert.Same(owner, target.CampHome);
        Assert.Null(target.Village);
        Assert.Equal(1.0, target.DefenceMultiplier);

        var stack = Assert.Single(target.Defence);
        Assert.Equal(("infantry", 2, 30), (stack.UnitType, stack.Level, stack.Count));
    }

    /// <summary>Відкликаний табір — уже не ціль: армія в дорозі додому.</summary>
    [Fact]
    public async Task ResolveAsync_ShouldNotFind_AMarchThatIsNotCamping()
    {
        var camp = GivenCamp(NewVillage("Табірне"), camping: false);

        await Assert.ThrowsAsync<EntityNotFoundException>(() =>
            Resolver().ResolveAsync(MarchTargetType.Camp, camp.Id, NewVillage("Нападник"), Now, CancellationToken.None));
    }

    [Fact]
    public async Task EnsureAttackAllowed_ShouldRefuse_TheOwnCamp()
    {
        var owner = NewVillage("Табірне");
        var camp = GivenCamp(owner);
        var resolver = Resolver();

        var target = await resolver.ResolveAsync(MarchTargetType.Camp, camp.Id, owner, Now, CancellationToken.None);

        var refusal = await Assert.ThrowsAsync<RequirementNotMetException>(() =>
            resolver.EnsureAttackAllowedAsync(owner, target, Now, CancellationToken.None));

        Assert.Equal(RefusalReasons.MarchOwnCamp.Key, refusal.Reason);
    }

    [Fact]
    public async Task EnsureAttackAllowed_ShouldPass_ForSomeoneElsesCamp()
    {
        var camp = GivenCamp(NewVillage("Табірне"));
        var attacker = NewVillage("Нападник");
        var resolver = Resolver();

        var target = await resolver.ResolveAsync(MarchTargetType.Camp, camp.Id, attacker, Now, CancellationToken.None);

        Assert.Null(await Record.ExceptionAsync(() =>
            resolver.EnsureAttackAllowedAsync(attacker, target, Now, CancellationToken.None)));
    }
}
