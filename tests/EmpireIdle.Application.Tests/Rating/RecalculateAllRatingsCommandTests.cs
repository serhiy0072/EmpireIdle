using EmpireIdle.Application.Interfaces;
using EmpireIdle.Application.Rating.Commands;
using EmpireIdle.Domain.Entities;
using EmpireIdle.Domain.Services;
using EmpireIdle.Domain.Services.Config;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Time.Testing;
using NSubstitute;

namespace EmpireIdle.Application.Tests.Rating;

/// <summary>
/// Годинний перерахунок читає світ цілком трьома вибірками. Тести фіксують
/// саме це: жодного запиту на гравця, і гравець без сили чи без рейтингу
/// не валить прогін.
/// </summary>
public class RecalculateAllRatingsCommandTests
{
    private static readonly DateTime Now = new(2026, 6, 1, 12, 0, 0, DateTimeKind.Utc);

    private readonly IPlayerRatingRepository _ratings = Substitute.For<IPlayerRatingRepository>();
    private readonly IPlayerPowerRepository _powers = Substitute.For<IPlayerPowerRepository>();
    private readonly IVillageRepository _villages = Substitute.For<IVillageRepository>();
    private readonly IUnitOfWork _unitOfWork = Substitute.For<IUnitOfWork>();

    private static GameConfig Config() => new()
    {
        Buildings = [new BuildingConfig { Key = "townhall", IsMainBuilding = true }],
        Rating = new RatingConfig
        {
            PowerWeight = 0.55,
            DevelopmentWeight = 0.25,
            ActivityWeight = 0.20,
            PowerReference = 100,
            DevelopmentReference = 100,
            ActivityReference = 100,
            Scale = 10_000
        }
    };

    private RecalculateAllRatingsCommandHandler Handler() => new(
        _ratings, _powers, _villages, _unitOfWork,
        new GameCatalog(Config()), new FakeTimeProvider(Now),
        NullLogger<RecalculateAllRatingsCommandHandler>.Instance);

    /// <summary>Село гравця з однією будівлею заданого рівня.</summary>
    private static Village VillageFor(Guid playerId, int buildingLevel = 1)
    {
        var configs = new Dictionary<string, BuildingConfig>
        {
            ["townhall"] = new() { Key = "townhall", IsMainBuilding = true }
        };

        var village = new Village(Guid.NewGuid(), playerId, "Test", ["food"], 0, 0);
        village.AddBuilding("townhall", configs, Now);

        var townhall = village.Buildings.Single();

        for (var level = 1; level < buildingLevel; level++)
        {
            townhall.BeginUpgrade(configs["townhall"], TimeSpan.Zero, Now,
                Domain.ValueObjects.ProductionBoost.None, locationMultiplier: 1.0);
            townhall.CompleteConstruction(Now);
        }

        return village;
    }

    private void Given(List<Village> villages, List<PlayerPower> powers, List<PlayerRating> ratings)
    {
        _villages.GetAllWithBuildingsAsync(Arg.Any<CancellationToken>()).Returns(villages);
        _powers.GetAllAsync(Arg.Any<CancellationToken>()).Returns(powers);
        _ratings.GetAllAsync(Arg.Any<CancellationToken>()).Returns(ratings);
    }

    /// <summary>Гравцю без рядка рейтингу він створюється при першому прогоні.</summary>
    [Fact]
    public async Task Handle_ShouldCreateRatingForNewPlayers()
    {
        var playerId = Guid.NewGuid();

        Given([VillageFor(playerId)], [], []);

        await Handler().Handle(new RecalculateAllRatingsCommand(), CancellationToken.None);

        await _ratings.Received(1).AddAsync(
            Arg.Is<PlayerRating>(r => r.PlayerId == playerId), Arg.Any<CancellationToken>());
    }

    /// <summary>Наявний рядок оновлюється, а не дублюється.</summary>
    [Fact]
    public async Task Handle_ShouldUpdateExistingRatings()
    {
        var playerId = Guid.NewGuid();
        var rating = new PlayerRating(Guid.NewGuid(), playerId, 1, Now.AddDays(-1));

        var power = new PlayerPower(Guid.NewGuid(), playerId, 1, Now);
        power.Set(army: 100, hero: 0, equipment: 0, Now);

        Given([VillageFor(playerId)], [power], [rating]);

        await Handler().Handle(new RecalculateAllRatingsCommand(), CancellationToken.None);

        await _ratings.DidNotReceive().AddAsync(Arg.Any<PlayerRating>(), Arg.Any<CancellationToken>());
        // 100 сили × 0.55 + 1 рівень будівлі × 0.25/100 = 5500 + 25
        Assert.Equal(5_525, rating.TotalRating);
        Assert.Equal(Now, rating.UpdatedAt);
    }

    /// <summary>
    /// Гравець без рядка сили отримує нуль за бойову вісь, а не виняток:
    /// перерахунок сили міг ще не відбутись жодного разу.
    /// </summary>
    [Fact]
    public async Task Handle_ShouldTreatMissingPowerAsZero()
    {
        var playerId = Guid.NewGuid();
        var rating = new PlayerRating(Guid.NewGuid(), playerId, 1, Now);

        Given([VillageFor(playerId, buildingLevel: 100)], [], [rating]);

        await Handler().Handle(new RecalculateAllRatingsCommand(), CancellationToken.None);

        Assert.Equal(0, rating.PowerScore);
        Assert.Equal(2_500, rating.TotalRating);
    }

    /// <summary>Рівні будівель дають вісь розвитку.</summary>
    [Fact]
    public async Task Handle_ShouldScoreDevelopmentFromBuildingLevels()
    {
        var playerId = Guid.NewGuid();
        var rating = new PlayerRating(Guid.NewGuid(), playerId, 1, Now);

        Given([VillageFor(playerId, buildingLevel: 50)], [], [rating]);

        await Handler().Handle(new RecalculateAllRatingsCommand(), CancellationToken.None);

        // 50 із 100 орієнтира × вага 0.25 × 10 000
        Assert.Equal(1_250, rating.TotalRating);
    }

    /// <summary>
    /// Один SaveChanges на весь світ, а не на гравця: рейтинг ніхто інший
    /// не пише, конфлікту не буде, а тисяча транзакцій щогодини дорожча.
    /// </summary>
    [Fact]
    public async Task Handle_ShouldSaveOnceForTheWholeWorld()
    {
        var players = Enumerable.Range(0, 5).Select(_ => Guid.NewGuid()).ToList();

        Given(players.Select(p => VillageFor(p)).ToList(), [], []);

        await Handler().Handle(new RecalculateAllRatingsCommand(), CancellationToken.None);

        await _unitOfWork.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    /// <summary>Читання світу — рівно три вибірки, без запиту на гравця.</summary>
    [Fact]
    public async Task Handle_ShouldReadTheWorldInThreeQueries()
    {
        var players = Enumerable.Range(0, 10).Select(_ => Guid.NewGuid()).ToList();

        Given(players.Select(p => VillageFor(p)).ToList(), [], []);

        await Handler().Handle(new RecalculateAllRatingsCommand(), CancellationToken.None);

        await _villages.Received(1).GetAllWithBuildingsAsync(Arg.Any<CancellationToken>());
        await _powers.Received(1).GetAllAsync(Arg.Any<CancellationToken>());
        await _ratings.Received(1).GetAllAsync(Arg.Any<CancellationToken>());
    }

    /// <summary>Порожній світ не валить джоб.</summary>
    [Fact]
    public async Task Handle_ShouldSurviveAnEmptyWorld()
    {
        Given([], [], []);

        await Handler().Handle(new RecalculateAllRatingsCommand(), CancellationToken.None);

        await _ratings.DidNotReceive().AddAsync(Arg.Any<PlayerRating>(), Arg.Any<CancellationToken>());
    }
}
