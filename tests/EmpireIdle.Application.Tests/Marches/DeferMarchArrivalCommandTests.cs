using EmpireIdle.Application.Interfaces;
using EmpireIdle.Application.Marches.Commands;
using EmpireIdle.Domain.Entities;
using EmpireIdle.Domain.Enums;
using EmpireIdle.Domain.ValueObjects;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Time.Testing;
using NSubstitute;

namespace EmpireIdle.Application.Tests.Marches;

/// <summary>Сканер відкладає прибуття, що впало, у свіжому scope — і не чіпає вже завершений похід.</summary>
public class DeferMarchArrivalCommandTests
{
    private static readonly DateTime Now = new(2026, 10, 26, 12, 0, 0, DateTimeKind.Utc);

    private readonly IMarchRepository _marches = Substitute.For<IMarchRepository>();
    private readonly IUnitOfWork _unitOfWork = Substitute.For<IUnitOfWork>();

    private DeferMarchArrivalCommandHandler Handler() => new(_marches, _unitOfWork, new FakeTimeProvider(Now),
        NullLogger<DeferMarchArrivalCommandHandler>.Instance);

    private March GivenMarch()
    {
        var march = new March(Guid.NewGuid(), 1, Guid.NewGuid(), 1, 1, 5, 5, MarchTargetType.Monster, Guid.NewGuid(),
            new Dictionary<UnitStackKey, int> { [new UnitStackKey("infantry", 1)] = 10 },
            Now.AddMinutes(-2), Now.AddMinutes(-20));

        _marches.GetByIdAsync(march.Id, Arg.Any<CancellationToken>()).Returns(march);

        return march;
    }

    [Fact]
    public async Task Handle_ShouldDeferTheArrival()
    {
        var march = GivenMarch();

        await Handler().Handle(new DeferMarchArrivalCommand(march.Id), CancellationToken.None);

        Assert.Equal(Now.AddMinutes(2), march.ArrivalRetryAt);
        await _unitOfWork.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_ShouldLeaveACompletedMarchAlone()
    {
        var march = GivenMarch();
        march.TurnBack(TimeSpan.Zero, Now);
        march.Complete(Now);

        await Handler().Handle(new DeferMarchArrivalCommand(march.Id), CancellationToken.None);

        Assert.Null(march.ArrivalRetryAt);
        await _unitOfWork.DidNotReceive().SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_ShouldSkipAVanishedMarch()
    {
        await Handler().Handle(new DeferMarchArrivalCommand(Guid.NewGuid()), CancellationToken.None);

        await _unitOfWork.DidNotReceive().SaveChangesAsync(Arg.Any<CancellationToken>());
    }
}
