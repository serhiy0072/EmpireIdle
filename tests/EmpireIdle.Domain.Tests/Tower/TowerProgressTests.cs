using EmpireIdle.Domain.Entities;
using EmpireIdle.Domain.Exceptions;
using EmpireIdle.Domain.Tower;

namespace EmpireIdle.Domain.Tests.Tower;

/// <summary>Прогрес вежі: поверхи строго по черзі, пройдений закривається назавжди, вершина — кінець.</summary>
public class TowerProgressTests
{
    private static readonly DateTime Now = new(2026, 10, 19, 12, 0, 0, DateTimeKind.Utc);

    private static TowerProgress Progress() => new(Guid.NewGuid(), Guid.NewGuid(), serverId: 1);

    [Fact]
    public void Clear_ShouldMoveToTheNextFloor_AndRememberWhen()
    {
        var progress = Progress();

        progress.Clear(1, maxFloor: 10, Now);

        Assert.Equal((1, 2, Now), (progress.HighestFloor, progress.NextFloor, progress.ReachedAt!.Value));
    }

    /// <summary>Пройдений поверх уже закритий, а через поверх не перестрибнути.</summary>
    [Theory]
    [InlineData(1)]
    [InlineData(3)]
    public void Clear_ShouldRefuseAnyFloorButTheNext(int floor)
    {
        var progress = Progress();
        progress.Clear(1, maxFloor: 10, Now);

        var refusal = Assert.Throws<RequirementNotMetException>(() => progress.Clear(floor, maxFloor: 10, Now));

        Assert.Equal(RefusalReasons.TowerFloorClosed.Key, refusal.Reason);
        Assert.Equal(1, progress.HighestFloor);
    }

    [Fact]
    public void EnsureCanStorm_ShouldRefuse_OnceTheTowerIsConquered()
    {
        var progress = Progress();
        progress.Clear(1, maxFloor: 1, Now);

        var refusal = Assert.Throws<RequirementNotMetException>(() => progress.EnsureCanStorm(2, maxFloor: 1));

        Assert.Equal(RefusalReasons.TowerConquered.Key, refusal.Reason);
    }

    /// <summary>Сід сталий для гравця й поверху і різний для сусідніх поверхів та різних гравців.</summary>
    [Fact]
    public void Seed_ShouldBeStablePerPlayerAndFloor()
    {
        var player = Guid.Parse("4f6c1a2e-9b3d-4e7a-8c1f-2d5e6a7b8c9d");

        Assert.Equal(TowerSeed.For(player, 7), TowerSeed.For(player, 7));
        Assert.NotEqual(TowerSeed.For(player, 7), TowerSeed.For(player, 8));
        Assert.NotEqual(TowerSeed.For(player, 7), TowerSeed.For(Guid.NewGuid(), 7));
    }
}
