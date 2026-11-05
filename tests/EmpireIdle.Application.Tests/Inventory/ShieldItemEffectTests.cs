using EmpireIdle.Application.Interfaces;
using EmpireIdle.Application.Inventory.Contracts;
using EmpireIdle.Application.Inventory.Effects;
using EmpireIdle.Domain.Entities;
using EmpireIdle.Domain.Exceptions;
using EmpireIdle.Domain.Services.Config;
using NSubstitute;

namespace EmpireIdle.Application.Tests.Inventory;

/// <summary>Щит предметом: новий — від моменту використання, чинний подовжується, напад знімає обидва.</summary>
public class ShieldItemEffectTests
{
    private static readonly DateTime Now = TestKit.Entities.Now;
    private static readonly Guid PlayerId = Guid.NewGuid();

    private readonly IVillageRepository _villages = Substitute.For<IVillageRepository>();
    private readonly Village _village = TestKit.Entities.VillageWithTownhall(townhallLevel: 5);

    public ShieldItemEffectTests()
        => _villages.GetByPlayerIdAsync(PlayerId, Arg.Any<CancellationToken>()).Returns(_village);

    private static ItemConfig Shield => new() { Key = "shield_8h", Type = "shield", DurationHours = 8 };

    private Task Use(int count = 1, DateTime? at = null)
        => new ShieldItemEffect(_villages).ApplyAsync(new ItemUsageContext(PlayerId, Shield, count, at ?? Now), CancellationToken.None);

    [Fact]
    public async Task Shield_ShouldStartNow_ForItsDuration_TimesTheCount()
    {
        await Use(count: 2);

        Assert.Equal(Now.AddHours(16), _village.ShieldUntil);
        Assert.True(_village.IsShieldedAt(Now.AddHours(15)));
        Assert.False(_village.IsShieldedAt(Now.AddHours(16)));
    }

    [Fact]
    public async Task Shield_ShouldExtend_AnActiveOne()
    {
        await Use();
        await Use(at: Now.AddHours(3));

        Assert.Equal(Now.AddHours(16), _village.ShieldUntil);
    }

    [Fact]
    public async Task Shield_ShouldStartAnew_AfterTheOldOneExpired()
    {
        await Use();
        await Use(at: Now.AddHours(20));

        Assert.Equal(Now.AddHours(28), _village.ShieldUntil);
    }

    [Fact]
    public async Task Shield_ShouldDrop_WhenThePlayerAttacks()
    {
        await Use();

        _village.DropShield(Now.AddHours(1));

        Assert.False(_village.IsShieldedAt(Now.AddHours(2)));
    }

    [Fact]
    public async Task Shield_ShouldRefuse_WithoutAVillage()
    {
        _villages.GetByPlayerIdAsync(PlayerId, Arg.Any<CancellationToken>()).Returns((Village?)null);

        await Assert.ThrowsAsync<EntityNotFoundException>(() => Use());
    }
}
