using EmpireIdle.Domain.Entities;
using EmpireIdle.Domain.Enums;
using EmpireIdle.Domain.ValueObjects;

namespace EmpireIdle.Domain.Tests.Entities;

/// <summary>Журнал гаманця розрізняє куплені й подаровані gems.</summary>
public class PlayerWalletTests
{
    private static readonly DateTime Now = new(2026, 9, 27, 12, 0, 0, DateTimeKind.Utc);

    [Fact]
    public void GrantGems_ShouldCreditTheBalance_AsARewardNotAPurchase()
    {
        var wallet = new PlayerWallet(Guid.NewGuid(), "user-1");

        wallet.GrantGems(new GemAmount(50), "quest:daily_1", Now);

        Assert.Equal(50, wallet.GemBalance.Value);
        var entry = Assert.Single(wallet.Transactions);
        Assert.Equal((TransactionType.GemReward, 50, "quest:daily_1"), (entry.Type, entry.Amount, entry.Reference));
    }

    [Fact]
    public void AddGems_ShouldStillBeLoggedAsAPurchase()
    {
        var wallet = new PlayerWallet(Guid.NewGuid(), "user-1");

        wallet.AddGems(new GemAmount(100), "cs_test_1", Now);

        Assert.Equal(TransactionType.GemPurchase, Assert.Single(wallet.Transactions).Type);
    }

    /// <summary>VIP рахується лише від покупок: нагороди й витрати лічильник куплених gems не чіпають.</summary>
    [Fact]
    public void PurchasedGems_ShouldCountOnlyPurchases()
    {
        var wallet = new PlayerWallet(Guid.NewGuid(), "user-1");

        wallet.AddGems(new GemAmount(100), "cs_1", Now);
        wallet.AddGems(new GemAmount(250), "cs_2", Now);
        wallet.GrantGems(new GemAmount(1000), "quest:daily_1", Now);
        wallet.SpendGems(new GemAmount(300), "speed-up", Guid.NewGuid(), Now);

        Assert.Equal(350, wallet.PurchasedGems);
    }

    /// <summary>Значення зберігаються числами в журналі — їх не можна переставляти.</summary>
    [Theory]
    [InlineData(TransactionType.GemPurchase, 0)]
    [InlineData(TransactionType.GemSpend, 1)]
    [InlineData(TransactionType.GemReward, 4)]
    public void TransactionType_ShouldKeepItsStoredValue(TransactionType type, int stored)
        => Assert.Equal(stored, (int)type);
}
