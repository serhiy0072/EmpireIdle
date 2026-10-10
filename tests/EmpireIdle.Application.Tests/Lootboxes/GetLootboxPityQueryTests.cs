using EmpireIdle.Application.Interfaces;
using EmpireIdle.Application.Lootboxes.Contracts;
using EmpireIdle.Application.Lootboxes.Queries;
using EmpireIdle.Domain.Entities;
using EmpireIdle.TestKit;
using NSubstitute;

namespace EmpireIdle.Application.Tests.Lootboxes;

public class GetLootboxPityQueryTests
{
    /// <summary>
    /// Лише скрині з гарантією; невідкрита — з нулем, відкрита — з лічильником. Мала скриня
    /// без гарантії у списку не з'являється.
    /// </summary>
    [Fact]
    public async Task Handle_ShouldListEveryPityChest_WithItsCounter()
    {
        var playerId = Guid.NewGuid();
        var lootboxes = Substitute.For<ILootboxRepository>();
        var catalog = new GameConfigBuilder().WithLootboxes().BuildCatalog();

        var pity = new LootboxPity(Guid.NewGuid(), playerId, TestKeys.BigChest);
        pity.Apply(2);
        lootboxes.GetPitiesReadOnlyAsync(playerId, Arg.Any<CancellationToken>()).Returns([pity]);

        var views = await new GetLootboxPityQueryHandler(lootboxes, catalog)
            .Handle(new GetLootboxPityQuery(playerId), CancellationToken.None);

        Assert.Equal([new LootboxPityView(TestKeys.BigChest, 2, 3)], views);
    }
}
