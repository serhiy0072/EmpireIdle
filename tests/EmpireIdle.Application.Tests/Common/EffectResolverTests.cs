using EmpireIdle.Application.Common.Services;
using EmpireIdle.Application.Interfaces;
using EmpireIdle.Domain.Entities;
using EmpireIdle.Domain.Enums;
using EmpireIdle.Domain.Services;
using EmpireIdle.TestKit;
using NSubstitute;

namespace EmpireIdle.Application.Tests.Common;

/// <summary>Бусти крамниці й пасивки звірів складаються додаванням (GDD §5.10).</summary>
public class EffectResolverTests
{
    private static readonly DateTime Now = new(2026, 10, 4, 12, 0, 0, DateTimeKind.Utc);
    private static readonly Guid PlayerId = Guid.NewGuid();

    private readonly IActiveEffectRepository _effects = Substitute.For<IActiveEffectRepository>();
    private readonly IBeastPenRepository _pens = Substitute.For<IBeastPenRepository>();

    /// <summary>Вовк у тестовому каталозі дає атаку: +15% на 1-му рівні.</summary>
    private readonly GameCatalog _catalog = new GameConfigBuilder().WithBeasts().BuildCatalog();

    private EffectResolver Resolver() => TestEffects.Resolver(_effects, _pens, _catalog);

    private BeastPen GivenActiveWolf(DateTime activatedAt)
    {
        var pen = new BeastPen(Guid.NewGuid(), PlayerId, 1, Now);
        pen.ResolveTaming(TestKeys.Beast, rolled: true, pityWins: 10, capacity: 1, maxRank: 5, Now);
        pen.Activate(TestKeys.Beast, _ => EffectTarget.Attack, TimeSpan.FromHours(2), TimeSpan.FromHours(8), activatedAt);

        _pens.GetByPlayerReadOnlyAsync(PlayerId, Arg.Any<CancellationToken>()).Returns(pen);
        return pen;
    }

    private void GivenShopBoost(EffectTarget target, double multiplier)
        => _effects.GetAsync(PlayerId, target, Arg.Any<CancellationToken>())
            .Returns(new ActiveEffect(Guid.NewGuid(), PlayerId, target, multiplier, Now.AddHours(-1), Now.AddHours(1), "boost"));

    [Fact]
    public async Task GetMultiplier_ShouldAddTheBeastToTheShopBoost()
    {
        GivenShopBoost(EffectTarget.Attack, 1.25);
        GivenActiveWolf(Now.AddMinutes(-10));

        Assert.Equal(1.40, await Resolver().GetMultiplierAsync(PlayerId, EffectTarget.Attack, Now), precision: 10);
    }

    [Fact]
    public async Task GetMultiplier_ShouldIgnoreAnExpiredBeast()
    {
        GivenActiveWolf(Now.AddHours(-3));

        Assert.Equal(1.0, await Resolver().GetMultiplierAsync(PlayerId, EffectTarget.Attack, Now), precision: 10);
    }

    [Fact]
    public async Task GetMultiplier_ShouldIgnoreABeastOfAnotherEffect()
    {
        GivenActiveWolf(Now.AddMinutes(-10));

        Assert.Equal(1.0, await Resolver().GetMultiplierAsync(PlayerId, EffectTarget.Defense, Now), precision: 10);
    }

    /// <summary>Вікно звіра стає окремим вікном виробітку — навіть прострочене: воно могло діяти частину періоду.</summary>
    [Fact]
    public async Task GetProductionBoost_ShouldAddTheBeastWindow()
    {
        var catalog = new GameConfigBuilder().WithBeasts(b => b.Types[0].Effect = EffectTarget.Production).BuildCatalog();
        var pen = new BeastPen(Guid.NewGuid(), PlayerId, 1, Now);
        pen.ResolveTaming(TestKeys.Beast, rolled: true, pityWins: 10, capacity: 1, maxRank: 5, Now);
        pen.Activate(TestKeys.Beast, _ => EffectTarget.Production, TimeSpan.FromHours(2), TimeSpan.FromHours(8), Now.AddHours(-3));
        _pens.GetByPlayerReadOnlyAsync(PlayerId, Arg.Any<CancellationToken>()).Returns(pen);
        GivenShopBoost(EffectTarget.Production, 2.0);

        var boost = await TestEffects.Resolver(_effects, _pens, catalog).GetProductionBoostAsync(PlayerId, Now);

        // Буст ×2 діяв останню годину, звір +15% — дві години до того
        Assert.Equal(60 + 0.15 * 120, boost.BonusMinutes(Now.AddHours(-3), Now), precision: 10);
    }
}
