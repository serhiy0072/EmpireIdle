using EmpireIdle.Application.Interfaces;
using EmpireIdle.Application.Wallets.Services;
using EmpireIdle.Domain.Entities;
using EmpireIdle.Domain.Services;
using EmpireIdle.Domain.Services.Config;
using EmpireIdle.Domain.ValueObjects;
using NSubstitute;

namespace EmpireIdle.Application.Tests;

/// <summary>VIP гравця для тестів: без покупок — нульовий рівень, або з купленими gems за порогами 100/200/300/400.</summary>
internal static class TestVip
{
    public static readonly MonetizationConfig Config = new() { VipThresholds = [100, 200, 300, 400], VipLevelsPerExtraMarch = 2 };

    /// <summary>Гравця не знайдено — VIP нічого не додає.</summary>
    public static PlayerVip None()
        => new(Substitute.For<IPlayerRepository>(), Substitute.For<IPlayerWalletRepository>(), new VipProgression(Config));

    /// <summary>Акаунт гравця купив стільки gems.</summary>
    public static PlayerVip Purchased(Guid playerId, int purchasedGems)
    {
        var players = Substitute.For<IPlayerRepository>();
        var wallets = Substitute.For<IPlayerWalletRepository>();
        var player = new Player(playerId, "vip", "vip@example.com", "vip-user", DateTime.UtcNow);
        var wallet = new PlayerWallet(Guid.NewGuid(), player.UserId);
        wallet.AddGems(new GemAmount(purchasedGems), "cs_vip", DateTime.UtcNow);

        players.GetByIdAsync(playerId, Arg.Any<CancellationToken>()).Returns(player);
        wallets.GetByUserIdAsync(player.UserId, Arg.Any<CancellationToken>()).Returns(wallet);

        return new PlayerVip(players, wallets, new VipProgression(Config));
    }
}
