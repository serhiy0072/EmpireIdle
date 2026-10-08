using EmpireIdle.Application.Dungeons.Services;
using EmpireIdle.Application.Interfaces;
using EmpireIdle.Domain.Entities;
using EmpireIdle.Domain.Enums;
using EmpireIdle.Domain.Services;
using EmpireIdle.Domain.Services.Config;
using EmpireIdle.TestKit;
using NSubstitute;

namespace EmpireIdle.Application.Tests.Dungeons;

/// <summary>
/// Вміння в бою знімаються на старті забігу (GDD §6.1, §6.5): лише відкриті рівнем героя
/// й лише ті, що мають ефект у данжі, — активне й періодичні, вже під свій рівень.
/// </summary>
public class DungeonTeamFactoryTests
{
    private static readonly Guid PlayerId = Guid.NewGuid();

    private readonly GameConfig _config = new GameConfigBuilder().WithUnits().WithDungeons().Build();
    private readonly IHeroRepository _heroes = Substitute.For<IHeroRepository>();
    private readonly IInventoryRepository _inventory = Substitute.For<IInventoryRepository>();

    public DungeonTeamFactoryTests()
    {
        var common = _config.Heroes.First(h => h.Key == TestKeys.CommonHero);

        common.Skills.Add(Passives.Attack(5));
        common.Skills.Add(new HeroSkillConfig
        {
            Key = "pulse", DisplayName = "Pulse", Half = SkillHalf.Attack, Kind = SkillKind.Periodic, UnlockLevel = 20,
            Troops = new SkillTroopBonusConfig { Stat = "Attack", Percents = [1, 2, 3, 4, 5, 6] },
            Battle = new SkillBattleConfig
            {
                Target = AbilityTarget.AllEnemies, Cooldown = 4, DamageMultiplier = 1, LevelScale = [1, 1.2, 1.4, 1.6, 1.8, 2],
            },
        });

        _inventory.GetEquippedAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>()).Returns([]);
    }

    private DungeonTeamFactory Factory()
    {
        var catalog = new GameCatalog(_config);
        var progression = new HeroProgression(_config.HeroSettings);

        return new DungeonTeamFactory(_heroes, _inventory, catalog, new HeroStats(progression, catalog), progression,
            new HeroSkills(_config.HeroSettings));
    }

    private Hero HeroOnDuty(int level)
    {
        var hero = Entities.Hero(TestKeys.CommonHero, PlayerId, level: level);
        _heroes.GetByIdAsync(hero.Id, Arg.Any<CancellationToken>()).Returns(hero);
        return hero;
    }

    /// <summary>Новий герой: лише активне на повній перезарядці; пасивка лишається війську, періодичне ще закрите.</summary>
    [Fact]
    public async Task BuildAsync_ShouldTakeOnlyTheUnlockedBattleSkills()
    {
        var team = await Factory().BuildAsync(PlayerId, [HeroOnDuty(level: 1).Id], CancellationToken.None);

        var skill = Assert.Single(team[0].Skills);
        Assert.Equal(TestKeys.DungeonAbility, skill.Key);
        Assert.Equal(SkillKind.Active, skill.Kind);
        Assert.Equal(1.8, skill.DamageMultiplier, 6);
        Assert.Equal(skill.Cooldown - 1, skill.CooldownLeft);
    }

    [Fact]
    public async Task BuildAsync_ShouldAddThePeriodic_OnceTheHeroLevelUnlocksIt()
    {
        var team = await Factory().BuildAsync(PlayerId, [HeroOnDuty(level: 20).Id], CancellationToken.None);

        Assert.Contains(team[0].Skills, s => s.Key == "pulse" && s.Kind == SkillKind.Periodic);
    }
}
