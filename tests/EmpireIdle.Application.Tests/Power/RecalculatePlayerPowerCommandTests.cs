using EmpireIdle.Application.Interfaces;
using EmpireIdle.Application.Power.Commands;
using EmpireIdle.Domain.Entities;
using MediatR;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;

namespace EmpireIdle.Application.Tests.Power;

/// <summary>Події героїв і спорядження знають лише гравця — команда знаходить його гарнізон.</summary>
public class RecalculatePlayerPowerCommandTests
{
    private static readonly Guid PlayerId = Guid.NewGuid();

    private readonly IVillageRepository _villages = Substitute.For<IVillageRepository>();
    private readonly IGarrisonRepository _garrisons = Substitute.For<IGarrisonRepository>();
    private readonly IMediator _mediator = Substitute.For<IMediator>();

    private RecalculatePlayerPowerCommandHandler Handler()
        => new(_villages, _garrisons, _mediator, NullLogger<RecalculatePlayerPowerCommandHandler>.Instance);

    [Fact]
    public async Task Handle_ShouldForwardToTheHomeGarrison()
    {
        var village = new Village(Guid.NewGuid(), PlayerId, "Test", ["food"], 0, 0);
        var garrison = new Garrison(Guid.NewGuid(), village.Id, 1);
        _villages.GetByPlayerIdReadOnlyAsync(PlayerId, Arg.Any<CancellationToken>()).Returns(village);
        _garrisons.GetByVillageIdAsync(village.Id, Arg.Any<CancellationToken>()).Returns(garrison);

        await Handler().Handle(new RecalculatePlayerPowerCommand(PlayerId), CancellationToken.None);

        await _mediator.Received(1).Send(
            Arg.Is<RecalculatePowerCommand>(c => c.GarrisonId == garrison.Id), Arg.Any<CancellationToken>());
    }

    /// <summary>Гравець без села — це не помилка, а «нема чого рахувати».</summary>
    [Fact]
    public async Task Handle_ShouldSkip_WhenThePlayerHasNoVillage()
    {
        _villages.GetByPlayerIdReadOnlyAsync(PlayerId, Arg.Any<CancellationToken>()).Returns((Village?)null);

        await Handler().Handle(new RecalculatePlayerPowerCommand(PlayerId), CancellationToken.None);

        await _mediator.DidNotReceive().Send(Arg.Any<RecalculatePowerCommand>(), Arg.Any<CancellationToken>());
    }
}
