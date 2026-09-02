using EmpireIdle.Application.Common.Events;
using EmpireIdle.Application.Interfaces;
using EmpireIdle.Application.Rating.Tracking;
using EmpireIdle.Domain.Entities;
using EmpireIdle.Domain.Events;
using NSubstitute;

namespace EmpireIdle.Application.Tests.Rating;

/// <summary>
/// Лічильники активності. Ключове — що вони інкрементують, а не заміщають,
/// і що відсутній рядок рейтингу не валить обробку події.
/// </summary>
public class RatingTrackingTests
{
    private static readonly DateTime Now = new(2026, 6, 1, 12, 0, 0, DateTimeKind.Utc);
    private static readonly Guid PlayerId = Guid.NewGuid();

    private readonly IPlayerRatingRepository _ratings = Substitute.For<IPlayerRatingRepository>();
    private readonly IUnitOfWork _unitOfWork = Substitute.For<IUnitOfWork>();

    private PlayerRating GivenRating()
    {
        var rating = new PlayerRating(Guid.NewGuid(), PlayerId, 1, Now);

        _ratings.GetByPlayerAsync(PlayerId, Arg.Any<CancellationToken>()).Returns(rating);

        return rating;
    }

    private void GivenNoRating()
        => _ratings.GetByPlayerAsync(PlayerId, Arg.Any<CancellationToken>()).Returns((PlayerRating?)null);

    // ---------- Монстри ----------

    [Fact]
    public async Task MonsterDefeated_ShouldIncrementTheCounter()
    {
        var rating = GivenRating();
        var handler = new RecordMonsterDefeated(_ratings, _unitOfWork);

        await handler.Handle(
            new DomainEventNotification<MonsterDefeated>(
                new MonsterDefeated(Guid.NewGuid(), PlayerId, Guid.NewGuid(), "wolves", 1, [], Now)),
            CancellationToken.None);

        Assert.Equal(1, rating.MonstersDefeated);
    }

    /// <summary>
    /// Гравець без рядка рейтингу не валить обробку: рядок створить
    /// найближчий прогін джоба, і лічильник почнеться з наступної події.
    /// </summary>
    [Fact]
    public async Task MonsterDefeated_ShouldDoNothing_WhenRatingIsMissing()
    {
        GivenNoRating();
        var handler = new RecordMonsterDefeated(_ratings, _unitOfWork);

        await handler.Handle(
            new DomainEventNotification<MonsterDefeated>(
                new MonsterDefeated(Guid.NewGuid(), PlayerId, Guid.NewGuid(), "wolves", 1, [], Now)),
            CancellationToken.None);

        await _unitOfWork.DidNotReceive().SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    // ---------- Бої ----------

    [Fact]
    public async Task BattleFought_ShouldCountAVictory()
    {
        var rating = GivenRating();
        var handler = new RecordBattleFought(_ratings, _unitOfWork);

        await handler.Handle(
            new DomainEventNotification<BattleFought>(
                new BattleFought(Guid.NewGuid(), PlayerId, Guid.NewGuid(), Guid.NewGuid(),
                    Won: true, "wolves", Now)),
            CancellationToken.None);

        Assert.Equal(1, rating.BattlesWon);
    }

    /// <summary>
    /// Поразка активності не додає: рейтинг міряє досягнення,
    /// а не кількість спроб.
    /// </summary>
    [Fact]
    public async Task BattleFought_ShouldIgnoreADefeat()
    {
        var rating = GivenRating();
        var handler = new RecordBattleFought(_ratings, _unitOfWork);

        await handler.Handle(
            new DomainEventNotification<BattleFought>(
                new BattleFought(Guid.NewGuid(), PlayerId, Guid.NewGuid(), Guid.NewGuid(),
                    Won: false, "wolves", Now)),
            CancellationToken.None);

        Assert.Equal(0, rating.BattlesWon);
        await _unitOfWork.DidNotReceive().SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    // ---------- Квести ----------

    [Fact]
    public async Task QuestCompleted_ShouldIncrementTheCounter()
    {
        var rating = GivenRating();
        var handler = new RecordQuestCompleted(_ratings, _unitOfWork);

        await handler.Handle(
            new DomainEventNotification<QuestCompleted>(
                new QuestCompleted(PlayerId, "daily_collect", Now)),
            CancellationToken.None);

        Assert.Equal(1, rating.QuestsCompleted);
    }

    /// <summary>Лічильники накопичуються між подіями, а не заміщаються.</summary>
    [Fact]
    public async Task Counters_ShouldAccumulateAcrossEvents()
    {
        var rating = GivenRating();
        var handler = new RecordMonsterDefeated(_ratings, _unitOfWork);

        for (var i = 0; i < 3; i++)
        {
            await handler.Handle(
                new DomainEventNotification<MonsterDefeated>(
                    new MonsterDefeated(Guid.NewGuid(), PlayerId, Guid.NewGuid(), "wolves", 1, [], Now)),
                CancellationToken.None);
        }

        Assert.Equal(3, rating.MonstersDefeated);
    }

    /// <summary>Різні види активності не змішуються.</summary>
    [Fact]
    public async Task Counters_ShouldStayIndependent()
    {
        var rating = GivenRating();

        await new RecordMonsterDefeated(_ratings, _unitOfWork).Handle(
            new DomainEventNotification<MonsterDefeated>(
                new MonsterDefeated(Guid.NewGuid(), PlayerId, Guid.NewGuid(), "wolves", 1, [], Now)),
            CancellationToken.None);

        await new RecordQuestCompleted(_ratings, _unitOfWork).Handle(
            new DomainEventNotification<QuestCompleted>(
                new QuestCompleted(PlayerId, "daily_collect", Now)),
            CancellationToken.None);

        Assert.Equal(1, rating.MonstersDefeated);
        Assert.Equal(1, rating.QuestsCompleted);
        Assert.Equal(0, rating.BattlesWon);
    }
}
