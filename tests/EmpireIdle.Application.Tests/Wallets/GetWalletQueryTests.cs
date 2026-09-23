using EmpireIdle.Application.Interfaces;
using EmpireIdle.Application.Wallets.Queries;
using EmpireIdle.Domain.Entities;
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

    private GetWalletQueryHandler Handler() => new(_wallets, _currentPlayer);

    [Fact]
    public async Task Handle_ShouldReturnTheWalletsGemBalance()
    {
        var wallet = new PlayerWallet(Guid.NewGuid(), UserId);
        wallet.AddGems(new GemAmount(250), "seed", PlayerId, DateTime.UtcNow);

        _currentPlayer.UserId.Returns(UserId);
        _wallets.GetByUserIdAsync(UserId, Arg.Any<CancellationToken>()).Returns(wallet);

        var response = await Handler().Handle(new GetWalletQuery(PlayerId), CancellationToken.None);

        Assert.Equal(250, response.GemBalance);
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
