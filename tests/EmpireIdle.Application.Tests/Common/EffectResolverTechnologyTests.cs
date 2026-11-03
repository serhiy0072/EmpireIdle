using EmpireIdle.Application.Interfaces;
using EmpireIdle.Domain.Entities;
using EmpireIdle.Domain.Enums;
using EmpireIdle.Domain.Services;
using EmpireIdle.Domain.Services.Config;
using EmpireIdle.TestKit;
using NSubstitute;

namespace EmpireIdle.Application.Tests.Common;

/// <summary>
/// Технології швидкості маршу й вантажу стають в один пул із бустами крамниці (рішення 28.10.2026).
/// Фікстура: attack_infantry тут ще й дає +2% швидкості маршу та +3% вантажу за рівень.
/// Атака й захист сюди не потрапляють — їх технології дають через бонус стека.
/// </summary>
public class EffectResolverTechnologyTests
{
    private static readonly DateTime Now = new(2026, 11, 3, 12, 0, 0, DateTimeKind.Utc);
    private static readonly Guid PlayerId = Guid.NewGuid();

    private readonly IActiveEffectRepository _effects = Substitute.For<IActiveEffectRepository>();
    private readonly IPlayerResearchRepository _research = Substitute.For<IPlayerResearchRepository>();

    private readonly GameCatalog _catalog = new GameConfigBuilder()
        .WithTechnologies(t => t.Personal.Nodes.Single(n => n.Key == "attack_infantry").Effects.AddRange(
        [
            new TechnologyEffectConfig { Stat = TechnologyStat.MarchSpeed, PerLevel = 0.02 },
            new TechnologyEffectConfig { Stat = TechnologyStat.Carry, PerLevel = 0.03 }
        ]))
        .BuildCatalog();

    private Task<double> Multiplier(EffectTarget target)
        => TestEffects.Resolver(_effects, catalog: _catalog, research: _research).GetMultiplierAsync(PlayerId, target, Now);

    [Fact]
    public async Task GetMultiplier_ShouldAddTheMarchSpeedAndCarryTechnologies()
    {
        _research.Researched(PlayerId, "attack_infantry", 2);

        Assert.Equal(1.04, await Multiplier(EffectTarget.MarchSpeed), precision: 10);
        Assert.Equal(1.06, await Multiplier(EffectTarget.Carry), precision: 10);
    }

    [Fact]
    public async Task GetMultiplier_ShouldAddTheTechnologyToTheShopBoost()
    {
        _research.Researched(PlayerId, "attack_infantry", 1);
        _effects.GetAsync(PlayerId, EffectTarget.MarchSpeed, Arg.Any<CancellationToken>())
            .Returns(new ActiveEffect(Guid.NewGuid(), PlayerId, EffectTarget.MarchSpeed, 1.5, Now.AddHours(-1), Now.AddHours(1), "boost"));

        Assert.Equal(1.52, await Multiplier(EffectTarget.MarchSpeed), precision: 10);
    }

    /// <summary>Атака піхоти вже діє в бонусі стека — у множнику атаки вона подвоїлася б.</summary>
    [Fact]
    public async Task GetMultiplier_ShouldLeaveAttackToTheStackBuff()
    {
        _research.Researched(PlayerId, "attack_infantry", 3);

        Assert.Equal(1.0, await Multiplier(EffectTarget.Attack), precision: 10);
    }
}
