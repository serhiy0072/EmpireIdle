using EmpireIdle.Application.Interfaces;
using EmpireIdle.Application.Inventory.Contracts;
using EmpireIdle.Application.Inventory.Effects;
using EmpireIdle.Domain.Entities;
using EmpireIdle.Domain.Enums;
using EmpireIdle.Domain.Exceptions;
using EmpireIdle.Domain.Services;
using EmpireIdle.Domain.Services.Config;
using EmpireIdle.TestKit;
using NSubstitute;

namespace EmpireIdle.Application.Tests.Inventory;

/// <summary>
/// Слабший прискорювач поверх сильнішого не спрацьовує — інакше гравець
/// спалив би предмет і втратив краще. Відмова має сказати, що саме вже діє.
/// </summary>
public class BoostItemEffectTests
{
    private static readonly DateTime Now = new(2026, 9, 23, 12, 0, 0, DateTimeKind.Utc);

    [Fact]
    public async Task Apply_AWeakerBoostOverAStrongerOne_ShouldNameWhatIsActive()
    {
        var playerId = Guid.NewGuid();
        var effects = Substitute.For<IActiveEffectRepository>();
        effects.GetAsync(playerId, EffectTarget.Attack, Arg.Any<CancellationToken>())
            .Returns(new ActiveEffect(Guid.NewGuid(), playerId, EffectTarget.Attack, 1.5, Now, Now.AddHours(2), "attack_boost_big"));

        var config = new GameConfigBuilder().WithBuildings().Build();
        var effect = new BoostItemEffect(effects, Substitute.For<IVillageRepository>(), Substitute.For<IServerRepository>(),
            new GameCatalog(config), new WorldGeometry(config.Map));

        var weaker = new ItemConfig { Key = "attack_boost_small", BoostTarget = "Attack", Multiplier = 1.2, DurationHours = 1 };

        var refusal = await Assert.ThrowsAsync<InvalidStateException>(() =>
            effect.ApplyAsync(new ItemUsageContext(playerId, weaker, 1, null, Now), CancellationToken.None));

        Assert.Equal(RefusalReasons.ItemStrongerBoostActive.Key, refusal.Reason);
        Assert.Equal(1.5, refusal.Args["multiplier"]);
        Assert.Equal("2026-09-23T14:00:00.0000000Z", refusal.Args["until"]);
    }
}
