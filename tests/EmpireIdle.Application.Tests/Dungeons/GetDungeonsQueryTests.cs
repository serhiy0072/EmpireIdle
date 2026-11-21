using EmpireIdle.Application.Dungeons.Contracts;
using EmpireIdle.Application.Dungeons.Queries;
using EmpireIdle.Application.Interfaces;
using EmpireIdle.Domain.Dungeons;
using EmpireIdle.Domain.Entities;
using EmpireIdle.Domain.Services;
using EmpireIdle.Domain.Services.Config;
using EmpireIdle.TestKit;
using Microsoft.Extensions.Time.Testing;
using NSubstitute;

namespace EmpireIdle.Application.Tests.Dungeons;

/// <summary>Вітрина данжів показує ціну наступного поповнення енергії й скільки їх лишилось на добу.</summary>
public class GetDungeonsQueryTests
{
    private static readonly DateTime Now = new(2026, 10, 12, 12, 0, 0, DateTimeKind.Utc);
    private static readonly Guid PlayerId = Guid.NewGuid();

    private readonly IDungeonRepository _dungeons = Substitute.For<IDungeonRepository>();
    private readonly IPlayerResearchRepository _research = Substitute.For<IPlayerResearchRepository>();
    private readonly GameConfig _config = new GameConfigBuilder().WithDungeons().Build();

    public GetDungeonsQueryTests()
    {
        _config.Dungeons.RefillPricesGems = [100, 300];
        _config.Dungeons.HeroExperiencePerRun = 1000;
        _dungeons.GetClearedLevelsAsync(PlayerId, Arg.Any<CancellationToken>()).Returns(new Dictionary<string, int>());
    }

    private Task<DungeonsOverview> Overview()
        => new GetDungeonsQueryHandler(_dungeons, Substitute.For<IVillageRepository>(), new BattleBuilder(_config.Dungeons),
                new GameCatalog(_config), new FakeTimeProvider(Now), new LocaleTexts(_config, "uk"),
                TestTechnologies.For(TestTechnologies.TreeWith(TechnologyStat.HeroExperience, 0.5), _research))
            .Handle(new GetDungeonsQuery(PlayerId), CancellationToken.None);

    /// <summary>Вітрина обіцяє рівно те, що видасть нагорода: без наставництва — база, із +50% — у півтора раза більше.</summary>
    [Fact]
    public async Task Handle_ShouldPromiseTheExperience_WithTheMentorshipTechnology()
    {
        var basic = (await Overview()).Dungeons[0].Levels[0].HeroExperience;

        _research.Researched(PlayerId, "attack_infantry", 1);

        var boosted = (await Overview()).Dungeons[0].Levels[0].HeroExperience;

        Assert.True(basic > 0, "у фікстурі рівень мусить давати досвід");
        Assert.Equal(BattleExperience.Boosted(basic, 0.5), boosted);
    }

    /// <summary>Перше поповнення доби — перша ціна, лишаються всі.</summary>
    [Fact]
    public async Task Handle_ShouldOfferTheFirstRefillPrice_WhenNoRefillWasMadeToday()
    {
        var overview = await Overview();

        Assert.Equal(100, overview.RefillPriceGems);
        Assert.Equal(2, overview.RefillsLeft);
    }

    /// <summary>Після поповнення — наступна ціна, на одне менше.</summary>
    [Fact]
    public async Task Handle_ShouldOfferTheNextPrice_AfterARefill()
    {
        var energy = new DungeonEnergy(Guid.NewGuid(), PlayerId, 0, Now);
        energy.RefillToCap(_config.Dungeons.MaxEnergy, _config.Dungeons.RegenHours, dailyLimit: 2, Now);
        _dungeons.GetEnergyAsync(PlayerId, Arg.Any<CancellationToken>()).Returns(energy);

        var overview = await Overview();

        Assert.Equal(300, overview.RefillPriceGems);
        Assert.Equal(1, overview.RefillsLeft);
    }

    /// <summary>Ліміт вичерпано — ціни немає.</summary>
    [Fact]
    public async Task Handle_ShouldOfferNoPrice_WhenTheDailyLimitIsReached()
    {
        _config.Dungeons.RefillPricesGems = [100];
        var energy = new DungeonEnergy(Guid.NewGuid(), PlayerId, 0, Now);
        energy.RefillToCap(_config.Dungeons.MaxEnergy, _config.Dungeons.RegenHours, dailyLimit: 1, Now);
        _dungeons.GetEnergyAsync(PlayerId, Arg.Any<CancellationToken>()).Returns(energy);

        var overview = await Overview();

        Assert.Null(overview.RefillPriceGems);
        Assert.Equal(0, overview.RefillsLeft);
    }
}
