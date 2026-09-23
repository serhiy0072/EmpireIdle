using EmpireIdle.Application.Banners.Commands;
using EmpireIdle.Application.Interfaces;
using EmpireIdle.Application.Rewards;
using EmpireIdle.Application.Rewards.Contracts;
using EmpireIdle.Domain.Entities;
using EmpireIdle.Domain.Enums;
using EmpireIdle.Domain.Exceptions;
using EmpireIdle.Domain.Services;
using EmpireIdle.Domain.Services.Config;
using EmpireIdle.Domain.ValueObjects;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Time.Testing;
using NSubstitute;

namespace EmpireIdle.Application.Tests.Banners;

public class RollBannerCommandTests
{
    private static readonly DateTime Now = new(2026, 9, 18, 12, 0, 0, DateTimeKind.Utc);
    private const string BannerKey = "test_banner";
    private const int Price = 150;

    private readonly IPlayerRepository _players = Substitute.For<IPlayerRepository>();
    private readonly IPlayerWalletRepository _wallets = Substitute.For<IPlayerWalletRepository>();
    private readonly IBannerRepository _banners = Substitute.For<IBannerRepository>();
    private readonly IUnitOfWork _unitOfWork = Substitute.For<IUnitOfWork>();
    private readonly IRewardGranter _granter = Substitute.For<IRewardGranter>();
    private readonly Guid _playerId = Guid.NewGuid();
    private readonly PlayerWallet _wallet;

    public RollBannerCommandTests()
    {
        var player = new Player(_playerId, "tester", "tester@example.com", "user-1", Now);
        _players.GetByIdAsync(_playerId, Arg.Any<CancellationToken>()).Returns(player);

        _wallet = new PlayerWallet(Guid.NewGuid(), "user-1");
        _wallet.AddGems(new GemAmount(1_000), "test", _playerId, Now);
        _wallets.GetByUserIdAsync("user-1", Arg.Any<CancellationToken>()).Returns(_wallet);

        _granter.RewardType.Returns("Gems");
    }

    private static BannerConfig Banner(DateTimeOffset? startsAt = null, DateTimeOffset? endsAt = null) => new()
    {
        Key = BannerKey,
        DisplayName = "Test banner",
        Kind = BannerKind.Hero,
        PityGroup = "hero",
        PriceGems = Price,
        RarePity = 10,
        UniquePity = 50,
        StartsAt = startsAt,
        EndsAt = endsAt,
        Drops =
        [
            new BannerDropConfig
            {
                Key = "rare_hero",
                DisplayName = "Rare hero",
                Rarity = Rarity.Rare,
                Kind = BannerKind.Hero,
                Weight = 1,
                Rewards = [new RewardConfig { Type = "Gems", Amount = 1 }]
            },
            new BannerDropConfig
            {
                Key = "unique_hero",
                DisplayName = "Unique hero",
                Rarity = Rarity.Unique,
                Kind = BannerKind.Hero,
                Weight = 1,
                Rewards = [new RewardConfig { Type = "Gems", Amount = 1 }]
            }
        ]
    };

    private RollBannerCommandHandler Handler(BannerConfig banner)
    {
        var random = Substitute.For<IRandomSource>();
        random.Next(Arg.Any<int>()).Returns(4242);

        var serverContext = Substitute.For<IServerContext>();
        serverContext.ServerId.Returns(1);

        return new RollBannerCommandHandler(
            _players,
            _wallets,
            _banners,
            new BannerRoller(new ShopConfig { Banners = [banner] }),
            new RewardDispatcher([_granter]),
            random,
            serverContext,
            _unitOfWork,
            new FakeTimeProvider(Now),
            NullLogger<RollBannerCommandHandler>.Instance);
    }

    private Task<BannerRollResponse> Roll(BannerConfig banner, int count = 1, BannerCurrency currency = BannerCurrency.Gems)
        => Handler(banner).Handle(new RollBannerCommand(_playerId, banner.Key, count, currency), CancellationToken.None);

    [Fact]
    public async Task Handle_ShouldChargeGemsAndGrantTheDrop()
    {
        var response = await Roll(Banner());

        Assert.Equal(1_000 - Price, _wallet.GemBalance.Value);
        Assert.Equal(1_000 - Price, response.GemBalance);
        await _granter.Received(1).GrantAsync(Arg.Any<RewardContext>(), Arg.Any<CancellationToken>());
        await _unitOfWork.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_ShouldStartAPityRow_OnTheFirstRoll()
    {
        BannerPityProgress? added = null;
        await _banners.AddPityAsync(Arg.Do<BannerPityProgress>(p => added = p), Arg.Any<CancellationToken>());

        await Roll(Banner());

        Assert.NotNull(added);
        Assert.Equal("hero", added!.PityGroup);
        Assert.Equal(1, added.TotalRolls);
    }

    /// <summary>Журнал зберігає сід і стан ДО ролла — без цього ролл не переграти.</summary>
    [Fact]
    public async Task Handle_ShouldJournalTheRoll()
    {
        BannerRollRecord? record = null;
        await _banners.AddRollAsync(Arg.Do<BannerRollRecord>(r => record = r), Arg.Any<CancellationToken>());

        var existing = new BannerPityProgress(Guid.NewGuid(), _playerId, "hero");
        existing.Apply(new PityState(RareSince: 4, UniqueSince: 4, FeaturedGuaranteed: false));
        _banners.GetPityAsync(_playerId, "hero", Arg.Any<CancellationToken>()).Returns(existing);

        var response = await Roll(Banner());

        Assert.NotNull(record);
        Assert.Equal(4242, record!.Seed);
        Assert.Equal(4, record.RareSinceBefore);
        Assert.Equal(Price, record.PriceGems);
        Assert.Equal(response.Drops[0].DropKey, record.DropKey);
    }

    /// <summary>Серія — одна транзакція: десять списань, десять записів журналу, один SaveChanges.</summary>
    [Fact]
    public async Task Handle_ShouldRollTheWholeSeries_InOneTransaction()
    {
        var response = await Roll(Banner(), count: 5);

        Assert.Equal(5, response.Drops.Count);
        Assert.Equal(1_000 - Price * 5, _wallet.GemBalance.Value);
        Assert.Equal(1_000 - Price * 5, response.GemBalance);
        await _banners.Received(5).AddRollAsync(Arg.Any<BannerRollRecord>(), Arg.Any<CancellationToken>());
        await _granter.Received(5).GrantAsync(Arg.Any<RewardContext>(), Arg.Any<CancellationToken>());
        await _unitOfWork.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    /// <summary>Гарантія рахується всередині серії: pity після серії відповідає п'яти окремим роллам.</summary>
    [Fact]
    public async Task Handle_ShouldAdvancePity_AcrossTheSeries()
    {
        BannerPityProgress? added = null;
        await _banners.AddPityAsync(Arg.Do<BannerPityProgress>(p => added = p), Arg.Any<CancellationToken>());

        await Roll(Banner(), count: 5);

        Assert.NotNull(added);
        Assert.Equal(5, added!.TotalRolls);
    }

    /// <summary>Gems вистачає на три роллі з п'яти — серія відхиляється цілком, нічого не списано.</summary>
    [Fact]
    public async Task Handle_ShouldReject_WhenGemsCoverOnlyPartOfTheSeries()
    {
        _wallet.SpendGems(new GemAmount(1_000 - Price * 3), "test", _playerId, Now);

        var error = await Assert.ThrowsAsync<NotEnoughResourcesException>(() => Roll(Banner(), count: 5));

        Assert.Equal(Price * 5, error.Need);
        Assert.Equal(Price * 3, _wallet.GemBalance.Value);
        await _granter.DidNotReceive().GrantAsync(Arg.Any<RewardContext>(), Arg.Any<CancellationToken>());
        await _unitOfWork.DidNotReceive().SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    /// <summary>Печатки призову — друга валюта банера: списуються замість gems, gems не чіпаються.</summary>
    [Fact]
    public async Task Handle_ShouldChargeSeals_WhenPayingWithSeals()
    {
        var banner = Banner();
        banner.PriceSeals = 40;
        _wallet.AddSeals(100, "test", Now);

        var response = await Roll(banner, count: 2, currency: BannerCurrency.Seals);

        Assert.Equal(100 - 80, _wallet.SealBalance);
        Assert.Equal(100 - 80, response.SealBalance);
        Assert.Equal(1_000, _wallet.GemBalance.Value);
    }

    /// <summary>Банер без ціни в печатках за печатки не крутять — це відмова, а не безкоштовний ролл.</summary>
    [Fact]
    public async Task Handle_ShouldReject_SealsOnABannerWithoutASealPrice()
    {
        _wallet.AddSeals(100, "test", Now);

        var refusal = await Assert.ThrowsAsync<RequirementNotMetException>(() => Roll(Banner(), currency: BannerCurrency.Seals));
        Assert.Null(refusal.Reason);

        Assert.Equal(100, _wallet.SealBalance);
    }

    [Fact]
    public async Task Handle_ShouldReject_WhenGemsAreShort()
    {
        _wallet.SpendGems(new GemAmount(900), "test", _playerId, Now);

        await Assert.ThrowsAsync<NotEnoughResourcesException>(() => Roll(Banner()));

        Assert.Equal(100, _wallet.GemBalance.Value);
        await _granter.DidNotReceive().GrantAsync(Arg.Any<RewardContext>(), Arg.Any<CancellationToken>());
        await _unitOfWork.DidNotReceive().SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_ShouldReject_AnUnknownBanner()
    {
        var handler = Handler(Banner());

        await Assert.ThrowsAsync<EntityNotFoundException>(
            () => handler.Handle(new RollBannerCommand(_playerId, "no_such_banner"), CancellationToken.None));
    }

    [Fact]
    public async Task Handle_ShouldReject_AClosedBanner()
    {
        var refusal = await Assert.ThrowsAsync<RequirementNotMetException>(
            () => Roll(Banner(endsAt: Now.AddDays(-1))));
        Assert.Equal(RefusalReasons.BannerClosed.Key, refusal.Reason);

        Assert.Equal(1_000, _wallet.GemBalance.Value);
    }

    [Fact]
    public async Task Handle_ShouldReject_ABannerThatHasNotOpened()
    {
        var refusal = await Assert.ThrowsAsync<RequirementNotMetException>(() => Roll(Banner(startsAt: Now.AddDays(1))));

        Assert.Equal(RefusalReasons.BannerNotOpen.Key, refusal.Reason);
        Assert.False(string.IsNullOrEmpty(refusal.Args["startsAt"] as string));
    }
}
