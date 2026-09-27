using EmpireIdle.Application.Battles.Queries;
using EmpireIdle.Application.Interfaces;
using EmpireIdle.Domain.Entities;
using NSubstitute;

namespace EmpireIdle.Application.Tests.Battles;

/// <summary>Звіти віддаються read model-ом, а не сутністю, і не більше стелі за раз.</summary>
public class GetBattleReportsQueryTests
{
    private static readonly DateTime Now = new(2026, 9, 27, 12, 0, 0, DateTimeKind.Utc);
    private static readonly Guid PlayerId = Guid.NewGuid();

    private readonly IBattleReportRepository _reports = Substitute.For<IBattleReportRepository>();

    private GetBattleReportsQueryHandler Handler() => new(_reports);

    [Fact]
    public async Task Handle_ShouldProjectTheReportWithItsLines()
    {
        var report = new BattleReport(Guid.NewGuid(), PlayerId, Guid.NewGuid(), 4, 7, "forest", "Вовки", 3,
            won: true, attackerPower: 120, defenderPower: 80, randomSeed: 42, Now);
        report.AddLine("infantry", sent: 10, wounded: 2, recoverable: 1, dead: 3);

        _reports.GetByPlayerAsync(PlayerId, 20, Arg.Any<CancellationToken>()).Returns([report]);

        var view = Assert.Single(await Handler().Handle(new GetBattleReportsQuery(PlayerId), CancellationToken.None));

        Assert.Equal((report.Id, "Вовки", true, Now), (view.Id, view.TargetName, view.Won, view.FoughtAt));

        var line = Assert.Single(view.Lines);
        Assert.Equal(("infantry", 10, 4, 2, 1, 3), (line.UnitType, line.Sent, line.Survived, line.Wounded, line.Recoverable, line.Dead));
    }

    [Theory]
    [InlineData(0, 1)]
    [InlineData(500, 50)]
    public async Task Handle_ShouldClampHowManyReportsItAsksFor(int requested, int expected)
    {
        _reports.GetByPlayerAsync(PlayerId, Arg.Any<int>(), Arg.Any<CancellationToken>()).Returns([]);

        await Handler().Handle(new GetBattleReportsQuery(PlayerId, requested), CancellationToken.None);

        await _reports.Received(1).GetByPlayerAsync(PlayerId, expected, Arg.Any<CancellationToken>());
    }
}
