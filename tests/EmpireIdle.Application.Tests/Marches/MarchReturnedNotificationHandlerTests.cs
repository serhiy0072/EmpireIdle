using EmpireIdle.Application.Common.Events;
using EmpireIdle.Application.Interfaces;
using EmpireIdle.Application.Marches.EventHandlers;
using EmpireIdle.Domain.Entities;
using EmpireIdle.Domain.Events;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;

namespace EmpireIdle.Application.Tests.Marches;

/// <summary>
/// Подія повернення знає лише гарнізон, а сповіщення адресується гравцю:
/// обробник має знайти власника через село, а не мовчки загубити подію.
/// </summary>
public class MarchReturnedNotificationHandlerTests
{
    private static readonly DateTime Now = new(2026, 6, 1, 12, 0, 0, DateTimeKind.Utc);

    private readonly IGarrisonRepository _garrisons = Substitute.For<IGarrisonRepository>();
    private readonly IVillageRepository _villages = Substitute.For<IVillageRepository>();
    private readonly IGameNotifier _notifier = Substitute.For<IGameNotifier>();

    private MarchReturnedNotificationHandler Handler() =>
        new(_garrisons, _villages, _notifier, NullLogger<MarchReturnedNotificationHandler>.Instance);

    [Fact]
    public async Task Handle_ShouldNotifyTheGarrisonOwner()
    {
        var playerId = Guid.NewGuid();
        var village = new Village(Guid.NewGuid(), playerId, "Test", [], 50, 50);
        var garrison = new Garrison(Guid.NewGuid(), village.Id, village.ServerId);
        var marchId = Guid.NewGuid();

        _garrisons.GetByIdAsync(garrison.Id, Arg.Any<CancellationToken>()).Returns(garrison);
        _villages.GetByIdAsync(village.Id, Arg.Any<CancellationToken>()).Returns(village);

        await Handler().Handle(
            new DomainEventNotification<MarchReturned>(new MarchReturned(marchId, garrison.Id, Now)),
            CancellationToken.None);

        await _notifier.Received(1).NotifyMarchReturnedAsync(playerId, marchId, Arg.Any<CancellationToken>());
    }

    /// <summary>Гарнізон без села — сповіщати нікого, але й падати обробнику нема чого.</summary>
    [Fact]
    public async Task Handle_ShouldSkipSilently_WhenTheVillageIsGone()
    {
        var garrisonId = Guid.NewGuid();

        _garrisons.GetByIdAsync(garrisonId, Arg.Any<CancellationToken>()).Returns((Garrison?)null);

        await Handler().Handle(
            new DomainEventNotification<MarchReturned>(new MarchReturned(Guid.NewGuid(), garrisonId, Now)),
            CancellationToken.None);

        await _notifier.DidNotReceiveWithAnyArgs().NotifyMarchReturnedAsync(default, default, default);
    }
}
