using EmpireIdle.Domain.Entities;

namespace EmpireIdle.Domain.Tests.Entities;

/// <summary>Лічильник гарантії скрині: записує стан від ролера й рахує відкриття.</summary>
public class LootboxPityTests
{
    [Fact]
    public void Apply_ShouldStoreTheCounter_AndCountTheOpening()
    {
        var pity = new LootboxPity(Guid.NewGuid(), Guid.NewGuid(), "chest");

        pity.Apply(1);
        pity.Apply(0);

        Assert.Equal((0, 2), (pity.Since, pity.TotalOpened));
    }

    [Fact]
    public void Apply_ShouldRejectANegativeCounter()
        => Assert.Throws<ArgumentOutOfRangeException>(() => new LootboxPity(Guid.NewGuid(), Guid.NewGuid(), "chest").Apply(-1));
}
