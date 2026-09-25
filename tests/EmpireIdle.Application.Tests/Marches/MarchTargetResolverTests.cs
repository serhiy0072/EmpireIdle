using EmpireIdle.Application.Interfaces;
using EmpireIdle.Application.Marches.Services;
using EmpireIdle.Domain.Combat;
using EmpireIdle.Domain.Entities;
using EmpireIdle.Domain.Exceptions;
using EmpireIdle.Domain.Services;
using EmpireIdle.Domain.Services.Config;
using EmpireIdle.Domain.ValueObjects;
using NSubstitute;

namespace EmpireIdle.Application.Tests.Marches;

/// <summary>
/// Щит новачка в атаці діє в обидва боки: хто під щитом, не атакує гравців
/// і сам недоторканний. Обидві відмови мають пояснювати гравцю, котрий це бік.
/// </summary>
public class MarchTargetResolverTests
{
    private static readonly DateTime Now = new(2026, 6, 1, 12, 0, 0, DateTimeKind.Utc);
    private const int ShieldLevel = 3;

    private static GameConfig Config() => new()
    {
        Buildings = [new BuildingConfig { Key = "townhall", IsMainBuilding = true, UpgradeCostGrowth = 1.45 }],
        Combat = new CombatConfig { NewbieShieldTownHallLevel = ShieldLevel }
    };

    private static MarchTargetResolver Resolver()
    {
        var catalog = new GameCatalog(Config());

        return new MarchTargetResolver(
            Substitute.For<IMonsterRepository>(),
            Substitute.For<IVillageRepository>(),
            Substitute.For<IGarrisonRepository>(),
            Substitute.For<IHeroRepository>(),
            new MonsterArmyBuilder(catalog),
            new HeroCombatModifiers(catalog),
            catalog,
            new VillageStatus(catalog),
            Substitute.For<IClanStructureRepository>(),
            Substitute.For<IClanRepository>(),
            new ClanTerritoryRules(catalog));
    }

    private static Village NewVillage(int townHallLevel)
    {
        var catalog = new GameCatalog(Config());
        var village = new Village(Guid.NewGuid(), Guid.NewGuid(), "Test", ["food"], 50, 50);

        village.AddBuilding("townhall", catalog.Buildings, Now);

        var townhall = village.Buildings.Single(b => b.Type == "townhall");

        for (var i = 1; i < townHallLevel; i++)
        {
            townhall.BeginUpgrade(catalog.Buildings["townhall"], TimeSpan.Zero, Now, ProductionBoost.None, 1.0);
            townhall.CompleteConstruction(Now);
        }

        return village;
    }

    private static MarchTarget TargetOf(Village village)
        => new(village.X, village.Y, village.Name, 5, village, [], DefenceBuffs.None, 1.0);

    [Fact]
    public void EnsureAttackAllowed_FromAShieldedVillage_ShouldNameTheLevelThatLiftsTheShield()
    {
        var refusal = Assert.Throws<RequirementNotMetException>(() =>
            Resolver().EnsureAttackAllowed(NewVillage(townHallLevel: 1), TargetOf(NewVillage(townHallLevel: 5)), Now));

        Assert.Equal(RefusalReasons.MarchOwnShield.Key, refusal.Reason);
        Assert.Equal(ShieldLevel, refusal.Args["level"]);
    }

    [Fact]
    public void EnsureAttackAllowed_AgainstAShieldedVillage_ShouldSayTheTargetIsProtected()
    {
        var refusal = Assert.Throws<RequirementNotMetException>(() =>
            Resolver().EnsureAttackAllowed(NewVillage(townHallLevel: 5), TargetOf(NewVillage(townHallLevel: 1)), Now));

        Assert.Equal(RefusalReasons.MarchTargetShielded.Key, refusal.Reason);
    }

    [Fact]
    public void EnsureAttackAllowed_BetweenUnshieldedVillages_ShouldPass()
    {
        var exception = Record.Exception(() =>
            Resolver().EnsureAttackAllowed(NewVillage(townHallLevel: 5), TargetOf(NewVillage(townHallLevel: 5)), Now));

        Assert.Null(exception);
    }

    /// <summary>Щойно впале село під щитом: відмова своя, не щит новачка.</summary>
    [Fact]
    public void EnsureAttackAllowed_AgainstAFallenVillage_ShouldRefuseWhileTheShieldHolds()
    {
        var target = NewVillage(townHallLevel: 5);
        var fall = new VillageFall(Guid.NewGuid(), 1, target.PlayerId, Guid.NewGuid(), "Нападник",
            target.X, target.Y, 60, 60, Now.AddHours(24), Now);
        target.RelocateTo(60, 60, Now);
        target.MarkFallen(fall, Now);

        var refusal = Assert.Throws<RequirementNotMetException>(() =>
            Resolver().EnsureAttackAllowed(NewVillage(townHallLevel: 5), TargetOf(target), Now.AddHours(1)));

        Assert.Equal(RefusalReasons.MarchTargetFallShield.Key, refusal.Reason);

        var afterShield = Record.Exception(() =>
            Resolver().EnsureAttackAllowed(NewVillage(townHallLevel: 5), TargetOf(target), Now.AddHours(24)));

        Assert.Null(afterShield);
    }
}
