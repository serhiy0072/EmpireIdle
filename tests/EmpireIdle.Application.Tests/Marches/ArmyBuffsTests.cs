using EmpireIdle.Application.Interfaces;
using EmpireIdle.Domain.Combat;
using EmpireIdle.Domain.Services;
using EmpireIdle.TestKit;
using NSubstitute;

namespace EmpireIdle.Application.Tests.Marches;

/// <summary>
/// Бойові технології в армійському бою (рішення 28.10.2026): кожен стек — під технологіями свого власника,
/// навіть без героя. Фікстура: attack_infantry тіру 1 дає +1% атаки піхоти за рівень.
/// </summary>
public class ArmyBuffsTests
{
    private readonly GameCatalog _catalog = new(new GameConfigBuilder().WithTechnologies().Build());
    private readonly IPlayerResearchRepository _research = Substitute.For<IPlayerResearchRepository>();

    private readonly Guid _host = Guid.NewGuid();
    private readonly Guid _ally = Guid.NewGuid();

    private void Researched(Guid playerId, int level) => _research.Researched(playerId, "attack_infantry", level);

    [Fact]
    public async Task ForArmyAsync_ShouldApplyTheOwnersTechnologies_WithoutAHero()
    {
        Researched(_host, 3);

        var buff = await TestCombatants.Buffs(_catalog, research: _research)
            .ForArmyAsync(_host, [], CancellationToken.None);

        Assert.Equal(1.03, buff.Attack(TestKeys.Infantry), 6);
        Assert.Equal(1.0, buff.Attack(TestKeys.Archer), 6);
    }

    [Fact]
    public async Task ForArmyAsync_ShouldGiveNothing_WhenTheOwnerHasNotResearched()
    {
        var buff = await TestCombatants.Buffs(_catalog, research: _research)
            .ForArmyAsync(_host, [], CancellationToken.None);

        Assert.Same(StackBuff.None, buff);
    }

    /// <summary>Господар і союзник без героя б'ються кожен під своїми технологіями.</summary>
    [Fact]
    public async Task ForDefenceAsync_ShouldGiveEveryStackItsOwnersTechnologies()
    {
        Researched(_host, 1);
        Researched(_ally, 2);

        var defence = new List<DefenceStack>
        {
            new(null, TestKeys.Infantry, 1, 100),
            new(_ally, TestKeys.Infantry, 1, 50)
        };

        var buffs = await TestCombatants.Buffs(_catalog, research: _research)
            .ForDefenceAsync(_host, [], [], defence, CancellationToken.None);

        Assert.Equal(1.01, buffs.For(null).Attack(TestKeys.Infantry), 6);
        Assert.Equal(1.02, buffs.For(_ally).Attack(TestKeys.Infantry), 6);
    }

    /// <summary>Споруда клану господаря не має: кожен учасник — під своїми технологіями.</summary>
    [Fact]
    public async Task ForDefenceAsync_ShouldHaveNoHostBonus_ForAStructure()
    {
        Researched(_ally, 2);

        var buffs = await TestCombatants.Buffs(_catalog, research: _research)
            .ForDefenceAsync(null, [], [], [new DefenceStack(_ally, TestKeys.Infantry, 1, 50)], CancellationToken.None);

        Assert.Same(StackBuff.None, buffs.For(null));
        Assert.Equal(1.02, buffs.For(_ally).Attack(TestKeys.Infantry), 6);
    }
}
