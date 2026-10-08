using EmpireIdle.Application.Heroes.Commands;
using EmpireIdle.Application.Heroes.Services;
using EmpireIdle.Application.Interfaces;
using EmpireIdle.Application.Inventory.Contracts;
using EmpireIdle.Application.Inventory.Effects;
using EmpireIdle.Application.Rewards.Contracts;
using EmpireIdle.Application.Rewards.Granters;
using EmpireIdle.Domain.Entities;
using EmpireIdle.Domain.Exceptions;
using EmpireIdle.Domain.Services;
using EmpireIdle.Domain.Services.Config;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Time.Testing;
using NSubstitute;

namespace EmpireIdle.Application.Tests.Heroes;

/// <summary>
/// Досвід героїв (GDD §6.1): копиться в пулі гравця — з баночок і нагород — і витрачається
/// на рівень обраного героя одразу, без черги. Скидання повертає все вкладене.
/// </summary>
public class HeroExperienceTests
{
    private static readonly DateTime Now = new(2026, 10, 6, 12, 0, 0, DateTimeKind.Utc);
    private static readonly Guid PlayerId = Guid.NewGuid();

    private readonly IHeroRepository _heroes = Substitute.For<IHeroRepository>();
    private readonly IUnitOfWork _unitOfWork = Substitute.For<IUnitOfWork>();
    private readonly IServerContext _serverContext = Substitute.For<IServerContext>();
    private readonly HeroProgression _progression = HeroTestConfig.Progression();
    private readonly List<Hero> _roster = [];
    private HeroExperiencePool? _pool;

    public HeroExperienceTests()
    {
        _serverContext.ServerId.Returns(1);
        _heroes.GetByPlayerAsync(PlayerId, Arg.Any<CancellationToken>()).Returns(_ => _roster.ToList());

        // Підміна, що поводиться як сховище: доданий пул видно наступним читанням
        _heroes.GetExperienceAsync(PlayerId, Arg.Any<CancellationToken>()).Returns(_ => _pool);
        _heroes.AddExperienceAsync(Arg.Any<HeroExperiencePool>(), Arg.Any<CancellationToken>())
            .Returns(call =>
            {
                _pool = call.Arg<HeroExperiencePool>();
                return Task.CompletedTask;
            });
    }

    private Hero GivenHero(int level = 1)
    {
        var hero = TestKit.Entities.Hero("warrior_bran", PlayerId, level: level);
        _heroes.GetByIdAsync(hero.Id, Arg.Any<CancellationToken>()).Returns(hero);
        _roster.Add(hero);
        return hero;
    }

    private void GivenExperience(long amount)
    {
        _pool = new HeroExperiencePool(Guid.NewGuid(), PlayerId, 1);
        if (amount > 0)
            _pool.Add(amount);
    }

    private TrainingCampService Camps()
        => new(_heroes, Substitute.For<IPlayerRepository>(), Substitute.For<IPlayerWalletRepository>(), _serverContext,
            new TrainingCampRules(HeroTestConfig.Create().HeroSettings.TrainingCamp));

    private Task LevelUp(Hero hero, int levels)
        => new LevelUpHeroCommandHandler(_heroes, _unitOfWork, _progression, HeroTestConfig.Catalog(), Camps(), new FakeTimeProvider(Now),
                NullLogger<LevelUpHeroCommandHandler>.Instance)
            .Handle(new LevelUpHeroCommand(PlayerId, hero.Id, levels), CancellationToken.None);

    private Task Reset(Hero hero)
        => new ResetHeroLevelCommandHandler(_heroes, _serverContext, _unitOfWork, _progression, Camps(), new FakeTimeProvider(Now),
                NullLogger<ResetHeroLevelCommandHandler>.Instance)
            .Handle(new ResetHeroLevelCommand(PlayerId, hero.Id), CancellationToken.None);

    // ---------- Підняття рівня ----------

    [Fact]
    public async Task LevelUp_ShouldRaiseTheHeroAtOnce_AndSpendTheExactCost()
    {
        var hero = GivenHero();
        var cost = _progression.ExperienceBetween(1, 6);
        GivenExperience(cost + 7);

        await LevelUp(hero, levels: 5);

        Assert.Equal(6, hero.Level);
        Assert.Equal(7, _pool!.Amount);
        await _unitOfWork.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    /// <summary>Не вистачає досвіду — відмова з цифрами, ні пул, ні герой не змінюються.</summary>
    [Fact]
    public async Task LevelUp_ShouldRefuse_WithoutEnoughExperience()
    {
        var hero = GivenHero();
        GivenExperience(_progression.ExperienceBetween(1, 6) - 1);

        await Assert.ThrowsAsync<NotEnoughResourcesException>(() => LevelUp(hero, levels: 5));

        Assert.Equal(1, hero.Level);
        Assert.Equal(_progression.ExperienceBetween(1, 6) - 1, _pool!.Amount);
    }

    /// <summary>Досвіду ще не було зовсім — та сама відмова, а не падіння на порожньому пулі.</summary>
    [Fact]
    public async Task LevelUp_ShouldRefuse_WithoutAPool()
    {
        var hero = GivenHero();

        await Assert.ThrowsAsync<NotEnoughResourcesException>(() => LevelUp(hero, levels: 1));
    }

    /// <summary>Стеля одна — MaxLevel; ратуша й тір її більше не задають.</summary>
    [Fact]
    public async Task LevelUp_ShouldRefuse_AboveTheMaxLevel()
    {
        var hero = GivenHero(level: _progression.MaxLevel);
        GivenExperience(long.MaxValue / 2);

        var refusal = await Assert.ThrowsAsync<RequirementNotMetException>(() => LevelUp(hero, levels: 1));

        Assert.Equal(RefusalReasons.HeroLevelCeiling.Key, refusal.Reason);
    }

    // ---------- Скидання ----------

    /// <summary>Скидання — на перший рівень, у пул повертається все вкладене.</summary>
    [Fact]
    public async Task Reset_ShouldReturnTheHeroToLevelOne_AndRefundAllTheExperience()
    {
        var hero = GivenHero(level: 20);
        GivenExperience(0);

        await Reset(hero);

        Assert.Equal(1, hero.Level);
        Assert.Equal(_progression.ExperienceBetween(1, 20), _pool!.Amount);
    }

    /// <summary>Пулу ще не було — скидання його створює, а не губить досвід.</summary>
    [Fact]
    public async Task Reset_ShouldCreateThePool_WhenThereIsNone()
    {
        var hero = GivenHero(level: 10);

        await Reset(hero);

        Assert.NotNull(_pool);
        Assert.Equal(_progression.ResetRefund(10), _pool!.Amount);
    }

    [Fact]
    public async Task Reset_ShouldRefuse_AHeroOnAMarch()
    {
        var hero = GivenHero(level: 10);
        hero.Deploy(Now);

        await Assert.ThrowsAsync<InvalidStateException>(() => Reset(hero));

        Assert.Equal(10, hero.Level);
    }

    // ---------- Джерела досвіду ----------

    /// <summary>Баночка додає досвід у пул, помножений на кількість відкритих.</summary>
    [Fact]
    public async Task Jar_ShouldAddItsExperienceToThePool()
    {
        var jar = new ItemConfig { Key = "hero_xp_small", Type = "heroxp", HeroExperience = 500 };

        await new HeroExperienceItemEffect(_heroes, _serverContext)
            .ApplyAsync(new ItemUsageContext(PlayerId, jar, 3, Now), CancellationToken.None);

        Assert.Equal(1500, _pool!.Amount);
    }

    /// <summary>Два рядки досвіду в одній нагороді — один пул, а не два (другий упав би на унікальному індексі).</summary>
    [Fact]
    public async Task Reward_ShouldAddToTheSamePool_Twice()
    {
        var granter = new HeroExperienceRewardGranter(_heroes, _serverContext);
        var reward = new RewardConfig { Type = "HeroExperience", Amount = 200 };

        await granter.GrantAsync(new RewardContext(PlayerId, reward, "quest", Now), CancellationToken.None);
        await granter.GrantAsync(new RewardContext(PlayerId, reward, "quest", Now), CancellationToken.None);

        Assert.Equal(400, _pool!.Amount);
        await _heroes.Received(1).AddExperienceAsync(Arg.Any<HeroExperiencePool>(), Arg.Any<CancellationToken>());
    }
}
