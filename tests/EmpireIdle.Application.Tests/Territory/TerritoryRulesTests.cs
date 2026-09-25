using EmpireIdle.Application.Interfaces;
using EmpireIdle.Application.Territory.Services;
using EmpireIdle.Domain.Entities;
using EmpireIdle.Domain.Enums;
using EmpireIdle.Domain.Exceptions;
using EmpireIdle.Domain.Services;
using EmpireIdle.Domain.Services.Config;
using EmpireIdle.TestKit;
using NSubstitute;

namespace EmpireIdle.Application.Tests.Territory;

/// <summary>Бонус території для села й правила маршів до споруд.</summary>
public class TerritoryRulesTests
{
    private static readonly DateTime Now = new(2026, 9, 25, 12, 0, 0, DateTimeKind.Utc);

    private readonly IClanRepository _clans = Substitute.For<IClanRepository>();
    private readonly IClanStructureRepository _structures = Substitute.For<IClanStructureRepository>();
    private readonly Guid _player = Guid.NewGuid();
    private readonly Guid _clanId = Guid.NewGuid();

    private static ClanTerritoryRules Rules(bool enabled = true)
    {
        var config = new GameConfigBuilder().WithBuildings().Build();
        config.Clan.Territory = new ClanTerritoryConfig { Enabled = enabled, Radius = 5, AttackBonus = 0.1, DefenceBonus = 0.2 };

        return new ClanTerritoryRules(new GameCatalog(config));
    }

    private Village VillageAt(int x, int y) => new(Guid.NewGuid(), _player, "Test", TestKeys.AllResources, x, y, 1);

    private ClanStructure Structure(Guid clanId, TimeSpan build)
        => new(Guid.NewGuid(), 1, clanId, 50, 50, Guid.NewGuid(), Guid.NewGuid(), build, Now.AddHours(-1));

    private TerritoryBonus Bonus(bool enabled = true) => new(_clans, _structures, Rules(enabled));

    [Fact]
    public async Task Bonus_ShouldApply_InsideAFinishedOwnStructure()
    {
        _clans.GetClanIdByMemberAsync(_player, Arg.Any<CancellationToken>()).Returns(_clanId);
        _structures.GetByClanAsync(_clanId, Arg.Any<CancellationToken>()).Returns([Structure(_clanId, TimeSpan.Zero)]);

        var village = VillageAt(53, 48);

        Assert.Equal(1.1, await Bonus().AttackMultiplierAsync(village, Now, CancellationToken.None), 6);
        Assert.Equal(1.2, await Bonus().DefenceMultiplierAsync(village, Now, CancellationToken.None), 6);
    }

    [Fact]
    public async Task Bonus_ShouldNotApply_OutsideTheRadius()
    {
        _clans.GetClanIdByMemberAsync(_player, Arg.Any<CancellationToken>()).Returns(_clanId);
        _structures.GetByClanAsync(_clanId, Arg.Any<CancellationToken>()).Returns([Structure(_clanId, TimeSpan.Zero)]);

        Assert.Equal(1, await Bonus().DefenceMultiplierAsync(VillageAt(60, 50), Now, CancellationToken.None));
    }

    [Fact]
    public async Task Bonus_ShouldNotApply_WhileTheStructureIsBuilding()
    {
        _clans.GetClanIdByMemberAsync(_player, Arg.Any<CancellationToken>()).Returns(_clanId);
        _structures.GetByClanAsync(_clanId, Arg.Any<CancellationToken>()).Returns([Structure(_clanId, TimeSpan.FromHours(10))]);

        Assert.Equal(1, await Bonus().AttackMultiplierAsync(VillageAt(50, 50), Now, CancellationToken.None));
    }

    /// <summary>Світ без території не ходить у базу на кожен бій.</summary>
    [Fact]
    public async Task Bonus_ShouldSkipTheLookup_WhenTerritoryIsOff()
    {
        Assert.Equal(1, await Bonus(enabled: false).AttackMultiplierAsync(VillageAt(50, 50), Now, CancellationToken.None));

        await _clans.DidNotReceive().GetClanIdByMemberAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Rules_ShouldRefuse_GarrisoningAForeignStructure()
    {
        _clans.GetClanIdByMemberAsync(_player, Arg.Any<CancellationToken>()).Returns(_clanId);

        var refusal = await Assert.ThrowsAsync<RequirementNotMetException>(() => new StructureMarchRules(_clans)
            .EnsureAllowedAsync(_player, Structure(Guid.NewGuid(), TimeSpan.Zero), MarchIntent.Reinforce, CancellationToken.None));

        Assert.Equal(RefusalReasons.TerritoryForeignStructure.Key, refusal.Reason);
    }

    [Fact]
    public async Task Rules_ShouldRefuse_AttackingOwnStructure()
    {
        _clans.GetClanIdByMemberAsync(_player, Arg.Any<CancellationToken>()).Returns(_clanId);

        var refusal = await Assert.ThrowsAsync<RequirementNotMetException>(() => new StructureMarchRules(_clans)
            .EnsureAllowedAsync(_player, Structure(_clanId, TimeSpan.Zero), MarchIntent.Attack, CancellationToken.None));

        Assert.Equal(RefusalReasons.TerritoryOwnStructure.Key, refusal.Reason);
    }

    [Theory]
    [InlineData(MarchIntent.Reinforce, true)]
    [InlineData(MarchIntent.Attack, false)]
    public async Task Rules_ShouldAllow_OwnGarrisonAndForeignAttack(MarchIntent intent, bool own)
    {
        _clans.GetClanIdByMemberAsync(_player, Arg.Any<CancellationToken>()).Returns(_clanId);
        var structure = Structure(own ? _clanId : Guid.NewGuid(), TimeSpan.Zero);

        var error = await Record.ExceptionAsync(() =>
            new StructureMarchRules(_clans).EnsureAllowedAsync(_player, structure, intent, CancellationToken.None));

        Assert.Null(error);
    }
}
