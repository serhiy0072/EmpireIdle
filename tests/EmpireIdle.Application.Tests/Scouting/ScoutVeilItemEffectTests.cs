using EmpireIdle.Application.Interfaces;
using EmpireIdle.Application.Inventory.Contracts;
using EmpireIdle.Application.Inventory.Effects;
using EmpireIdle.Domain.Entities;
using EmpireIdle.Domain.Enums;
using EmpireIdle.Domain.Services.Config;
using NSubstitute;

namespace EmpireIdle.Application.Tests.Scouting;

/// <summary>Завіса від розвідки: нова — з моменту використання, діюча — подовжується, строки складаються.</summary>
public class ScoutVeilItemEffectTests
{
    private static readonly DateTime Now = TestKit.Entities.Now;
    private static readonly Guid PlayerId = Guid.NewGuid();

    private readonly IActiveEffectRepository _effects = Substitute.For<IActiveEffectRepository>();

    private static ItemConfig Veil => new() { Key = "scout_veil_24h", Type = "scoutveil", DurationHours = 24 };

    private Task Use(int count = 1)
        => new ScoutVeilItemEffect(_effects).ApplyAsync(new ItemUsageContext(PlayerId, Veil, count, null, Now), CancellationToken.None);

    [Fact]
    public async Task Veil_ShouldStartNow_ForItsDuration()
    {
        ActiveEffect? added = null;
        await _effects.AddAsync(Arg.Do<ActiveEffect>(e => added = e), Arg.Any<CancellationToken>());

        await Use(count: 2);

        Assert.NotNull(added);
        Assert.Equal(EffectTarget.ScoutBlock, added.Target);
        Assert.Equal(Now.AddHours(48), added.ExpiresAt);
    }

    [Fact]
    public async Task Veil_ShouldExtendAnActiveOne()
    {
        var active = new ActiveEffect(Guid.NewGuid(), PlayerId, EffectTarget.ScoutBlock, 1.0, Now.AddHours(-1), Now.AddHours(5), "scout_veil_24h");
        _effects.GetAsync(PlayerId, EffectTarget.ScoutBlock, Arg.Any<CancellationToken>()).Returns(active);

        await Use();

        Assert.Equal(Now.AddHours(29), active.ExpiresAt);
        await _effects.DidNotReceiveWithAnyArgs().AddAsync(default!, default);
    }

    [Fact]
    public async Task Veil_ShouldRestartAnExpiredOne()
    {
        var expired = new ActiveEffect(Guid.NewGuid(), PlayerId, EffectTarget.ScoutBlock, 1.0, Now.AddDays(-2), Now.AddDays(-1), "scout_veil_24h");
        _effects.GetAsync(PlayerId, EffectTarget.ScoutBlock, Arg.Any<CancellationToken>()).Returns(expired);

        await Use();

        Assert.Equal(Now.AddHours(24), expired.ExpiresAt);
        Assert.True(expired.IsActive(Now));
    }
}
