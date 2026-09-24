using EmpireIdle.Application.Interfaces;
using EmpireIdle.Application.LoginRewards.Commands;
using EmpireIdle.Application.Mail.Commands;
using EmpireIdle.Application.Mail.Services;
using EmpireIdle.Application.Rewards;
using EmpireIdle.Application.Rewards.Contracts;
using EmpireIdle.Domain.Entities;
using EmpireIdle.Domain.Enums;
using EmpireIdle.Domain.Exceptions;
using EmpireIdle.Domain.Services;
using EmpireIdle.Domain.Services.Config;
using EmpireIdle.Domain.ValueObjects;
using EmpireIdle.TestKit;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Time.Testing;
using NSubstitute;

namespace EmpireIdle.Application.Tests.LoginRewards;

/// <summary>
/// Нагороди за вхід (GDD §8.12) доходять листами й забираються з них
/// тим самим диспетчером, що й квестові.
/// </summary>
public class LoginRewardsTests
{
    // Середа, 23 вересня 2026, 10:00 UTC
    private static readonly DateTime Now = new(2026, 9, 23, 10, 0, 0, DateTimeKind.Utc);
    private static readonly Guid PlayerId = Guid.NewGuid();

    private readonly ILoginRewardRepository _progress = Substitute.For<ILoginRewardRepository>();
    private readonly IMailRepository _mail = Substitute.For<IMailRepository>();
    private readonly IServerContext _serverContext = Substitute.For<IServerContext>();
    private readonly IUnitOfWork _unitOfWork = Substitute.For<IUnitOfWork>();
    private readonly IRewardGranter _gems = Substitute.For<IRewardGranter>();
    private readonly List<MailLetter> _sent = [];

    public LoginRewardsTests()
    {
        _serverContext.ServerId.Returns(1);
        _gems.RewardType.Returns("Gems");
        _mail.AddLetterAsync(Arg.Do<MailLetter>(_sent.Add), Arg.Any<CancellationToken>());
    }

    private static GameCatalog Catalog(bool withDaily = true)
    {
        var config = new GameConfigBuilder().WithBuildings().Build();
        config.LoginRewards = new LoginRewardsConfig
        {
            Daily = withDaily
                ? [.. Enumerable.Range(1, 7).Select(day => new LoginRewardDayConfig
                    { Rewards = [new RewardConfig { Type = "Gems", Amount = day }] })]
                : [],
            Weekly = [new RewardConfig { Type = "Gems", Amount = 100 }],
            Monthly = []
        };

        return new GameCatalog(config);
    }

    private CheckInCommandHandler CheckIn(GameCatalog? catalog = null) => new(_progress, _mail, _serverContext,
        _unitOfWork, catalog ?? Catalog(), new FakeTimeProvider(Now), NullLogger<CheckInCommandHandler>.Instance);

    private MailRewardClaimer Claimer() => new(new RewardDispatcher([_gems]));

    // ---------- Вхід ----------

    /// <summary>Перший вхід: лист дня й лист тижня; порожній місяць листа не дає.</summary>
    [Fact]
    public async Task CheckIn_ShouldMailTheDailyAndWeeklyRewards_OnTheFirstVisit()
    {
        var view = await CheckIn().Handle(new CheckInCommand(PlayerId), CancellationToken.None);

        Assert.Equal(1, view.DailyDay);
        Assert.Equal(2, view.Letters);

        var daily = Assert.Single(_sent, l => l.Kind == MailKind.DailyReward);
        Assert.Equal(1, daily.Sequence);
        Assert.Equal(new DateTime(2026, 9, 24, 0, 0, 0, DateTimeKind.Utc), daily.ExpiresAt);

        var weekly = Assert.Single(_sent, l => l.Kind == MailKind.WeeklyReward);
        Assert.Equal(new DateTime(2026, 9, 28, 0, 0, 0, DateTimeKind.Utc), weekly.ExpiresAt);

        await _progress.Received(1).AddAsync(Arg.Any<LoginRewardProgress>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task CheckIn_ShouldMailNothing_OnASecondVisitTheSameDay()
    {
        var progress = new LoginRewardProgress(Guid.NewGuid(), PlayerId, 1);
        progress.CheckIn(LoginCalendar.Day(Now), 7);
        _progress.GetByPlayerAsync(PlayerId, Arg.Any<CancellationToken>()).Returns(progress);

        var view = await CheckIn().Handle(new CheckInCommand(PlayerId), CancellationToken.None);

        Assert.Equal(0, view.Letters);
        Assert.Empty(_sent);
    }

    [Fact]
    public async Task CheckIn_ShouldSkipTheDailyLetter_WhenNoSeriesIsConfigured()
    {
        await CheckIn(Catalog(withDaily: false)).Handle(new CheckInCommand(PlayerId), CancellationToken.None);

        Assert.DoesNotContain(_sent, l => l.Kind == MailKind.DailyReward);
    }

    // ---------- Отримання ----------

    private MailLetter Letter(int gems, Guid? owner = null)
        => MailLetter.WithRewards(Guid.NewGuid(), 1, owner ?? PlayerId, MailKind.DailyReward,
            [new MailReward("Gems", null, gems)], 1, Now.AddHours(-1), Now.AddHours(10));

    [Fact]
    public async Task ClaimAll_ShouldHandOverEveryLetterAndSumTheRewards()
    {
        var letters = new List<MailLetter> { Letter(10), Letter(15) };
        _mail.GetClaimableLettersAsync(PlayerId, Now, Arg.Any<CancellationToken>()).Returns(letters);

        var view = await new ClaimAllRewardsCommandHandler(_mail, _unitOfWork, Claimer(), new FakeTimeProvider(Now))
            .Handle(new ClaimAllRewardsCommand(PlayerId), CancellationToken.None);

        Assert.Equal(2, view.Letters);
        Assert.Equal(25, Assert.Single(view.Rewards).Amount);
        Assert.All(letters, l => Assert.True(l.IsClaimed));
        await _gems.Received(2).GrantAsync(Arg.Any<RewardContext>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Claim_ShouldRefuse_SomeoneElsesLetter()
    {
        var letter = Letter(10, owner: Guid.NewGuid());
        _mail.GetLetterAsync(letter.Id, Arg.Any<CancellationToken>()).Returns(letter);

        await Assert.ThrowsAsync<EntityNotFoundException>(() =>
            new ClaimLetterRewardsCommandHandler(_mail, _unitOfWork, Claimer(), new FakeTimeProvider(Now))
                .Handle(new ClaimLetterRewardsCommand(PlayerId, letter.Id), CancellationToken.None));

        Assert.False(letter.IsClaimed);
        await _gems.DidNotReceive().GrantAsync(Arg.Any<RewardContext>(), Arg.Any<CancellationToken>());
    }
}
