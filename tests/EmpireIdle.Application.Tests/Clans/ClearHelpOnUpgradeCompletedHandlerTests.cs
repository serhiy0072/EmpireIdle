using EmpireIdle.Application.Clans.EventHandlers;
using EmpireIdle.Application.Common.Events;
using EmpireIdle.Application.Interfaces;
using EmpireIdle.Domain.Events;
using EmpireIdle.Domain.ValueObjects;
using NSubstitute;

namespace EmpireIdle.Application.Tests.Clans;

/// <summary>Завершений апгрейд прибирає запит допомоги на цю будівлю.</summary>
public class ClearHelpOnUpgradeCompletedHandlerTests
{
    [Fact]
    public async Task Handle_ShouldRemoveEveryRequestForTheBuilding()
    {
        var help = Substitute.For<IClanHelpRepository>();
        var buildingId = Guid.NewGuid();
        var completed = new BuildingUpgradeCompleted(Guid.NewGuid(), Guid.NewGuid(), buildingId, "townhall", new BuildingLevel(5), DateTime.UtcNow);

        await new ClearHelpOnUpgradeCompletedHandler(help)
            .Handle(new DomainEventNotification<BuildingUpgradeCompleted>(completed), CancellationToken.None);

        await help.Received(1).RemoveForTargetAsync(buildingId, null, Arg.Any<CancellationToken>());
    }
}
