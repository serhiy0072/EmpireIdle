using EmpireIdle.Domain.Entities;
using EmpireIdle.Domain.Enums;
using EmpireIdle.Domain.Events;
using EmpireIdle.Domain.ValueObjects;

namespace EmpireIdle.Domain.Tests.Entities;

/// <summary>
/// Тривога про напад народжується разом із маршем: захисник дізнається до прибуття.
/// Монстри, підкріплення й повернення тривоги не піднімають.
/// </summary>
public class MarchAlertTests
{
    private static readonly DateTime Now = new(2026, 9, 25, 12, 0, 0, DateTimeKind.Utc);

    private static readonly Dictionary<UnitStackKey, int> Army = new() { [new UnitStackKey("infantry", 1)] = 10 };

    private static March Send(MarchTargetType targetType, MarchIntent intent, Guid targetId)
        => new(Guid.NewGuid(), 1, Guid.NewGuid(), Guid.NewGuid(), 0, 0, 5, 5,
            targetType, targetId, Army, Now.AddMinutes(20), Now, intent);

    [Theory]
    [InlineData(MarchTargetType.Village)]
    [InlineData(MarchTargetType.ClanStructure)]
    public void Attack_OnADefender_ShouldRaiseHostileMarchLaunched(MarchTargetType targetType)
    {
        var targetId = Guid.NewGuid();

        var march = Send(targetType, MarchIntent.Attack, targetId);

        var launched = Assert.IsType<HostileMarchLaunched>(Assert.Single(march.DomainEvents));
        Assert.Equal(march.Id, launched.MarchId);
        Assert.Equal(targetType, launched.TargetType);
        Assert.Equal(targetId, launched.TargetId);
        Assert.Equal(Now.AddMinutes(20), launched.ArrivesAt);
    }

    [Fact]
    public void Attack_OnAMonster_ShouldRaiseNothing()
    {
        var march = Send(MarchTargetType.Monster, MarchIntent.Attack, Guid.NewGuid());

        Assert.Empty(march.DomainEvents);
    }

    [Theory]
    [InlineData(MarchTargetType.Village)]
    [InlineData(MarchTargetType.ClanStructure)]
    public void Reinforcement_ShouldRaiseNothing(MarchTargetType targetType)
    {
        var march = Send(targetType, MarchIntent.Reinforce, Guid.NewGuid());

        Assert.Empty(march.DomainEvents);
    }

    [Fact]
    public void ReturningHome_ShouldRaiseNothing()
    {
        var march = March.ReturningHome(Guid.NewGuid(), 1, Guid.NewGuid(), null, 0, 0, 5, 5, Guid.NewGuid(),
            Army, TimeSpan.FromMinutes(10), Now);

        Assert.Empty(march.DomainEvents);
    }

    /// <summary>Поселення нападника переїхало — напад зірвано, захисники мають зняти тривогу.</summary>
    [Fact]
    public void RecallAfterRelocation_OfAnAttack_ShouldRaiseHostileMarchCalledOff()
    {
        var targetId = Guid.NewGuid();
        var march = Send(MarchTargetType.Village, MarchIntent.Attack, targetId);
        march.ClearDomainEvents();

        march.RecallAfterRelocation(1, 1, Now.AddMinutes(5));

        var calledOff = Assert.IsType<HostileMarchCalledOff>(Assert.Single(march.DomainEvents));
        Assert.Equal(march.Id, calledOff.MarchId);
        Assert.Equal(targetId, calledOff.TargetId);
    }

    [Fact]
    public void RecallAfterRelocation_OfAMonsterHunt_ShouldRaiseNothing()
    {
        var march = Send(MarchTargetType.Monster, MarchIntent.Attack, Guid.NewGuid());

        march.RecallAfterRelocation(1, 1, Now.AddMinutes(5));

        Assert.Empty(march.DomainEvents);
    }

    /// <summary>Нога починається з виходу, а з розворотом — заново: від неї клієнт веде загін.</summary>
    [Fact]
    public void LegStartedAt_ShouldRestart_WhenTheMarchTurnsBack()
    {
        var march = Send(MarchTargetType.Monster, MarchIntent.Attack, Guid.NewGuid());
        Assert.Equal(Now, march.LegStartedAt);

        march.TurnBack(TimeSpan.FromMinutes(20), Now.AddMinutes(20));

        Assert.Equal(Now.AddMinutes(20), march.LegStartedAt);
    }

    /// <summary>Розвідка — теж ворожий марш: ціль бачить його й отримує тривогу з ім'ям.</summary>
    [Fact]
    public void Scouting_ShouldRaiseHostileMarchLaunched()
    {
        var march = Send(MarchTargetType.Village, MarchIntent.Scout, Guid.NewGuid());

        Assert.IsType<HostileMarchLaunched>(Assert.Single(march.DomainEvents));
    }

    /// <summary>Розвідники звітують на місці й назад не йдуть.</summary>
    [Fact]
    public void FinishScouting_ShouldCompleteTheMarch_AndOnlyForScouts()
    {
        var scouts = Send(MarchTargetType.Village, MarchIntent.Scout, Guid.NewGuid());
        var attack = Send(MarchTargetType.Village, MarchIntent.Attack, Guid.NewGuid());

        scouts.FinishScouting(Now.AddMinutes(20));

        Assert.Equal(MarchState.Completed, scouts.State);
        Assert.ThrowsAny<Exception>(() => attack.FinishScouting(Now.AddMinutes(20)));
    }

    [Fact]
    public void ScoutReport_ShouldKeepOnlyPositiveLoot_AndRejectAFailedSuccess()
    {
        var march = Send(MarchTargetType.Village, MarchIntent.Scout, Guid.NewGuid());

        var report = ScoutReport.Success(Guid.NewGuid(), 1, Guid.NewGuid(), march, "Ціль", 120.5,
            new Dictionary<string, int> { ["food"] = 300, ["wood"] = 0 }, Now);

        Assert.Equal(("food", 300), Assert.Single(report.Resources.Select(r => (r.ResourceType, r.Amount))));
        Assert.Throws<ArgumentException>(() => ScoutReport.Failed(Guid.NewGuid(), 1, Guid.NewGuid(), march, "Ціль",
            ScoutOutcome.Success, Now));
    }
}
