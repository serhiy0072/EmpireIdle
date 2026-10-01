using EmpireIdle.Domain.Entities;
using EmpireIdle.Domain.Enums;
using EmpireIdle.Domain.Events;
using EmpireIdle.Domain.Exceptions;
using EmpireIdle.Domain.ValueObjects;

namespace EmpireIdle.Domain.Tests.Entities;

/// <summary>
/// Табір (§2.5): атака, що не застала села на місці, стоїть на клітинці,
/// доки власник не відкличе, — і не доганяє село.
/// </summary>
public class MarchCampTests
{
    private static readonly DateTime Now = new(2026, 9, 26, 12, 0, 0, DateTimeKind.Utc);

    private static readonly Dictionary<UnitStackKey, int> Army = new() { [new UnitStackKey("infantry", 1)] = 10 };

    private static March Send(MarchIntent intent = MarchIntent.Attack, MarchTargetType targetType = MarchTargetType.Village)
        => new(Guid.NewGuid(), 1, Guid.NewGuid(), Guid.NewGuid(), 0, 0, 5, 6,
            targetType, Guid.NewGuid(), Army, Now.AddMinutes(20), Now, intent);

    /// <summary>Захисникам знято тривогу, власнику — де стоїть табір.</summary>
    [Fact]
    public void Camp_ShouldStayOnTheCell_AndTellBothSides()
    {
        var march = Send();
        march.ClearDomainEvents();

        march.Camp(Now.AddMinutes(20));

        Assert.Equal(MarchState.Camping, march.State);
        Assert.Equal(Now.AddMinutes(20), march.LegStartedAt);
        Assert.Contains(march.DomainEvents, e => e is HostileMarchCalledOff calledOff && calledOff.MarchId == march.Id);

        var camped = Assert.Single(march.DomainEvents.OfType<MarchCamped>());
        Assert.Equal((5, 6), (camped.X, camped.Y));
        Assert.Equal(march.GarrisonId, camped.GarrisonId);
    }

    /// <summary>Табором стає лише атака: підкріплення вертається, розвідник звітує.</summary>
    [Theory]
    [InlineData(MarchIntent.Reinforce)]
    [InlineData(MarchIntent.Scout)]
    public void Camp_ShouldBeRefused_ForAnythingButAnAttack(MarchIntent intent)
    {
        var march = Send(intent);

        Assert.Throws<InvalidStateException>(() => march.Camp(Now.AddMinutes(20)));
    }

    /// <summary>Відкликаний табір іде додому звичайним маршем — на актуальні координати дому.</summary>
    [Fact]
    public void BreakCamp_ShouldTurnTheCampIntoAMarchHome()
    {
        var march = Send();
        march.Camp(Now.AddMinutes(20));

        march.BreakCamp(9, 9, TimeSpan.FromMinutes(15), Now.AddHours(2));

        Assert.Equal(MarchState.Returning, march.State);
        Assert.Equal(Now.AddHours(2).AddMinutes(15), march.ArrivesAt);
        Assert.Equal(Now.AddHours(2), march.LegStartedAt);
        Assert.Equal((9, 9), (march.OriginX, march.OriginY));
    }

    [Fact]
    public void BreakCamp_ShouldBeRefused_WhenTheMarchIsNotCamping()
    {
        var march = Send();

        var refusal = Assert.Throws<InvalidStateException>(() =>
            march.BreakCamp(0, 0, TimeSpan.FromMinutes(15), Now.AddMinutes(5)));

        Assert.Equal(RefusalReasons.MarchNotCamping.Key, refusal.Reason);
    }

    /// <summary>Табір стоїть на місці — прискорювати нічого.</summary>
    [Fact]
    public void ReduceTravelTime_ShouldBeRefused_ForACamp()
    {
        var march = Send();
        march.Camp(Now.AddMinutes(20));

        var refusal = Assert.Throws<InvalidStateException>(() =>
            march.ReduceTravelTime(TimeSpan.FromMinutes(1), Now.AddMinutes(21)));

        Assert.Equal(RefusalReasons.MarchCamping.Key, refusal.Reason);
    }

    /// <summary>
    /// Власник переїхав — табір теж одразу вдома. Тривогу вдруге не знімаємо:
    /// її зняли, коли він став.
    /// </summary>
    [Fact]
    public void CallHomeAtOnce_ShouldBringACampHome_WithoutASecondCallOff()
    {
        var march = Send();
        march.Camp(Now.AddMinutes(20));
        march.ClearDomainEvents();

        march.CallHomeAtOnce(3, 3, Now.AddHours(1));

        Assert.Equal(MarchState.Returning, march.State);
        Assert.Equal(Now.AddHours(1), march.ArrivesAt);
        Assert.Empty(march.DomainEvents);
    }
}
