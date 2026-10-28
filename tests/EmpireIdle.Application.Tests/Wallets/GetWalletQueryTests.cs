using EmpireIdle.Application.Interfaces;
using EmpireIdle.Application.Wallets.Queries;
using EmpireIdle.Domain.Entities;
using EmpireIdle.Domain.Services;
using EmpireIdle.Domain.Services.Config;
using EmpireIdle.Domain.ValueObjects;
using NSubstitute;

namespace EmpireIdle.Application.Tests.Wallets;

/// <summary>
/// Баланс gems — окремий запит від села: гаманець акаунтний,
/// а не прив'язаний до конкретного села гравця на сервері.
/// </summary>
public class GetWalletQueryTests
{
    private static readonly Guid PlayerId = Guid.NewGuid();
    private const string UserId = "user-1";

    private readonly IPlayerWalletRepository _wallets = Substitute.For<IPlayerWalletRepository>();
    private readonly ICurrentPlayer _currentPlayer = Substitute.For<ICurrentPlayer>();

    private readonly VipProgression _vip = new(new MonetizationConfig { VipThresholds = [100, 300, 600] });

    private GetWalletQueryHandler Handler() => new(_wallets, _currentPlayer, _vip);

    [Fact]
    public async Task Handle_ShouldReturnTheWalletsGemBalance()
    {
        var wallet = new PlayerWallet(Guid.NewGuid(), UserId);
        wallet.AddGems(new GemAmount(250), "seed", DateTime.UtcNow);

        _currentPlayer.UserId.Returns(UserId);
        _wallets.GetByUserIdAsync(UserId, Arg.Any<CancellationToken>()).Returns(wallet);

        var response = await Handler().Handle(new GetWalletQuery(PlayerId), CancellationToken.None);

        Assert.Equal(250, response.GemBalance);
    }

    /// <summary>VIP рахується від куплених gems: 350 куплених — другий рівень, до третього треба 600.</summary>
    [Fact]
    public async Task Handle_ShouldReportTheVipLevelOfPurchasedGems()
    {
        var wallet = new PlayerWallet(Guid.NewGuid(), UserId);
        wallet.AddGems(new GemAmount(350), "cs_1", DateTime.UtcNow);
        wallet.GrantGems(new GemAmount(5000), "quest:big", DateTime.UtcNow);

        _currentPlayer.UserId.Returns(UserId);
        _wallets.GetByUserIdAsync(UserId, Arg.Any<CancellationToken>()).Returns(wallet);

        var response = await Handler().Handle(new GetWalletQuery(PlayerId), CancellationToken.None);

        Assert.Equal((350L, 2, (long?)600), (response.PurchasedGems, response.VipLevel, response.NextVipThreshold));
    }

    /// <summary>Без автентифікованого акаунта питати баланс нізвідки.</summary>
    [Fact]
    public async Task Handle_ShouldThrow_WhenNoAccountIsAuthenticated()
    {
        _currentPlayer.UserId.Returns((string?)null);

        await Assert.ThrowsAsync<UnauthorizedAccessException>(() =>
            Handler().Handle(new GetWalletQuery(PlayerId), CancellationToken.None));
    }

    /// <summary>Гравець без гаманця — битий стан, а не нульовий баланс.</summary>
    [Fact]
    public async Task Handle_ShouldThrow_WhenWalletIsMissing()
    {
        _currentPlayer.UserId.Returns(UserId);
        _wallets.GetByUserIdAsync(UserId, Arg.Any<CancellationToken>()).Returns((PlayerWallet?)null);

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            Handler().Handle(new GetWalletQuery(PlayerId), CancellationToken.None));
    }
}
