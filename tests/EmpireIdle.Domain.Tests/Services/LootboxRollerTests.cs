using EmpireIdle.Domain.Services;
using EmpireIdle.Domain.Services.Config;
using EmpireIdle.TestKit;

namespace EmpireIdle.Domain.Tests.Services;

/// <summary>
/// Розіграш скрині (GDD §9.12): ваги таблиці, гарантія рівно на своєму відкритті
/// й лічильник, що скидається, щойно позиція гарантії випала.
/// </summary>
public class LootboxRollerTests
{
    private static readonly List<LootboxConfig> Boxes = new GameConfigBuilder().WithLootboxes().Build().Lootboxes;

    private static LootboxConfig Small => Boxes.Single(b => b.Key == TestKeys.SmallChest);
    private static LootboxConfig Big => Boxes.Single(b => b.Key == TestKeys.BigChest);

    /// <summary>Шанси в порядку таблиці: вага / сума ваг.</summary>
    [Fact]
    public void Odds_ShouldFollowTheWeights_InTableOrder()
    {
        Assert.Equal([75.0, 25.0], LootboxRoller.Odds(Small));
        Assert.Equal([60.0, 30.0, 10.0], LootboxRoller.Odds(Big));
    }

    /// <summary>Шанс у соті частки відсотка не губиться в округленні: 1 з 4002 — це 0,025%.</summary>
    [Fact]
    public void Odds_ShouldKeepThreeDecimals_ForTheRarestEntries()
    {
        var box = new LootboxConfig
        {
            Key = "rare_odds",
            Entries =
            [
                new LootboxEntryConfig { Type = "Gems", Amount = 1, Weight = 4000 },
                new LootboxEntryConfig { Type = "Gems", Amount = 2, Weight = 1 },
                new LootboxEntryConfig { Type = "Gems", Amount = 3, Weight = 1 }
            ]
        };

        Assert.Equal([99.95, 0.025, 0.025], LootboxRoller.Odds(box));
    }

    /// <summary>Тисячі сідів дають частоти, близькі до ваг.</summary>
    [Fact]
    public void Roll_ShouldPickByWeight()
    {
        const int rolls = 8_000;

        var gold = Enumerable.Range(0, rolls)
            .Count(seed => LootboxRoller.Roll(Small, 0, seed).Entry.Type == "Resource");

        Assert.InRange(gold / (double)rolls, 0.72, 0.78);
    }

    [Fact]
    public void Roll_ShouldBeReplayable_FromTheSeed()
    {
        var first = LootboxRoller.Roll(Big, 1, seed: 42);
        var second = LootboxRoller.Roll(Big, 1, seed: 42);

        Assert.Same(first.Entry, second.Entry);
        Assert.Equal(first.PitySince, second.PitySince);
    }

    /// <summary>На відкритті PityWithin поспіль — лише позиція гарантії, хоч яким був би кидок.</summary>
    [Fact]
    public void Roll_ShouldForceThePityEntry_OnTheLastOpening()
    {
        foreach (var seed in Enumerable.Range(0, 200))
        {
            var roll = LootboxRoller.Roll(Big, pitySince: 2, seed);

            Assert.True(roll.Entry.Pity);
            Assert.True(roll.WasPity);
            Assert.Equal(0, roll.PitySince);
        }
    }

    /// <summary>
    /// До гарантії: позиція гарантії, що випала сама, скидає лічильник, але гарантією не вважається;
    /// будь-яка інша — додає одиницю.
    /// </summary>
    [Fact]
    public void Roll_ShouldCountUp_UntilThePityEntryDrops()
    {
        var rolls = Enumerable.Range(0, 500).Select(seed => LootboxRoller.Roll(Big, pitySince: 1, seed)).ToList();

        Assert.All(rolls, roll =>
        {
            Assert.False(roll.WasPity);
            Assert.Equal(roll.Entry.Pity ? 0 : 2, roll.PitySince);
        });
        Assert.Contains(rolls, roll => roll.Entry.Pity);
    }

    /// <summary>Скриня без гарантії не веде лічильник — він лишається нулем.</summary>
    [Fact]
    public void Roll_ShouldKeepTheCounterAtZero_WithoutPity()
        => Assert.All(Enumerable.Range(0, 50), seed => Assert.Equal(0, LootboxRoller.Roll(Small, 0, seed).PitySince));
}
