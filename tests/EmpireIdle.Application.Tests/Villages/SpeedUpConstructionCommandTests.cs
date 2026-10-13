using EmpireIdle.Application.Common.Security;
using EmpireIdle.Application.Interfaces;
using EmpireIdle.Application.Villages.Commands;
using EmpireIdle.Domain.Exceptions;
using EmpireIdle.Domain.Services;
using EmpireIdle.Domain.Services.Config;
using EmpireIdle.TestKit;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Time.Testing;
using NSubstitute;

namespace EmpireIdle.Application.Tests.Villages;

/// <summary>
/// Прискорення будівництва за gems. Таймер може добігти кінця між кліком і
/// запитом — це не збій, а відмова з поясненням «вже завершено».
/// </summary>
public class SpeedUpConstructionCommandTests
{
    [Fact]
    public async Task Handle_OnACompletedBuilding_ShouldSayItIsAlreadyDone()
    {
        var village = Entities.VillageWithTownhall(townhallLevel: 1);
        var farm = village.Buildings.Single(b => b.Type == TestKeys.Farm);

        var villages = Substitute.For<IVillageRepository>();
        villages.GetByPlayerIdAsync(village.PlayerId, Arg.Any<CancellationToken>()).Returns(village);

        var handler = new SpeedUpConstructionCommandHandler(
            villages,
            Substitute.For<IPlayerWalletRepository>(),
            Substitute.For<ICurrentPlayer>(),
            Substitute.For<IUnitOfWork>(),
            new SpeedUpCalculator(new MonetizationConfig()),
            new GameCatalog(new GameConfigBuilder().WithBuildings(TestKeys.Farm).Build()),
            new FakeTimeProvider(Entities.Now),
            NullLogger<SpeedUpConstructionCommandHandler>.Instance);

        var refusal = await Assert.ThrowsAsync<InvalidStateException>(() => handler.Handle(
            new SpeedUpConstructionCommand(village.PlayerId, farm.Id), CancellationToken.None));

        Assert.Equal(RefusalReasons.BuildingAlreadyCompleted.Key, refusal.Reason);
    }
}
