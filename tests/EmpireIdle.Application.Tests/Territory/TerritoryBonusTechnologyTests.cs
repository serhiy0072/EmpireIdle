using EmpireIdle.Application.Interfaces;
using EmpireIdle.Application.Territory.Services;
using EmpireIdle.Domain.Entities;
using EmpireIdle.Domain.Services;
using EmpireIdle.Domain.Services.Config;
using EmpireIdle.TestKit;
using NSubstitute;

namespace EmpireIdle.Application.Tests.Territory;

/// <summary>
/// Технології «Земель» підсилюють бонус території: членам — за їхнім кланом, споруді — за кланом-власником.
/// Бонус території тут 10%, рівень v_attack_all дає ще +1 в.п. до атаки й +2 в.п. до оборони.
/// </summary>
public class TerritoryBonusTechnologyTests
{
    private static readonly DateTime Now = Entities.Now;
    private static readonly Guid PlayerId = Guid.NewGuid();
    private static readonly Guid ClanId = Guid.NewGuid();

    private readonly IClanRepository _clans = Substitute.For<IClanRepository>();
    private readonly IClanStructureRepository _structures = Substitute.For<IClanStructureRepository>();
    private readonly IClanResearchRepository _clanResearch = TestTechnologies.NoClan();
    private readonly TerritoryBonus _bonus;
    private readonly Village _village = Entities.Village(PlayerId, x: 10, y: 10);

    public TerritoryBonusTechnologyTests()
    {
        var config = new GameConfigBuilder()
            .WithTechnologies(t => t.Clan.Nodes.Single(n => n.Key == "v_attack_all").Effects.AddRange(
            [
                new TechnologyEffectConfig { Stat = TechnologyStat.TerritoryAttack, PerLevel = 0.01 },
                new TechnologyEffectConfig { Stat = TechnologyStat.TerritoryDefense, PerLevel = 0.02 }
            ]))
            .Build();
        config.Clan.Territory = new ClanTerritoryConfig { Enabled = true, Radius = 5, AttackBonus = 0.1, DefenceBonus = 0.1 };

        _bonus = TestTechnologies.TerritoryBonus(new GameCatalog(config), _clans, _structures, _clanResearch);

        _clans.GetClanIdByMemberAsync(PlayerId, Arg.Any<CancellationToken>()).Returns(ClanId);

        var levels = (IReadOnlyDictionary<string, int>?)new Dictionary<string, int> { ["1:v_attack_all"] = 1 };
        _clanResearch.GetLevelsByMemberAsync(PlayerId, Arg.Any<CancellationToken>()).Returns(levels);
        _clanResearch.GetLevelsByClanAsync(ClanId, Arg.Any<CancellationToken>()).Returns(levels);
    }

    /// <summary>Добудована споруда на (x, y).</summary>
    private ClanStructure Structure(int x, int y)
        => new(Guid.NewGuid(), 1, ClanId, x, y, Guid.NewGuid(), PlayerId, TimeSpan.Zero, Now.AddDays(-1));

    private void GivenStructures(params ClanStructure[] structures)
        => _structures.GetByClanAsync(ClanId, Arg.Any<CancellationToken>()).Returns(structures.ToList());

    [Fact]
    public async Task Multipliers_ShouldIncludeTheTechnology_InsideTheTerritory()
    {
        GivenStructures(Structure(11, 11));

        Assert.Equal(1.11, await _bonus.AttackMultiplierAsync(_village, Now, CancellationToken.None), 6);
        Assert.Equal(1.12, await _bonus.DefenceMultiplierAsync(_village, Now, CancellationToken.None), 6);
    }

    [Fact]
    public async Task Multipliers_ShouldBeOne_OutsideTheTerritory_WhateverTheClanHasLearned()
    {
        GivenStructures(Structure(90, 90));

        Assert.Equal(1, await _bonus.AttackMultiplierAsync(_village, Now, CancellationToken.None));
        Assert.Equal(1, await _bonus.DefenceMultiplierAsync(_village, Now, CancellationToken.None));
    }

    [Fact]
    public async Task StructureDefence_ShouldUseTheOwningClansTechnology_OnceBuilt()
    {
        var built = Structure(50, 50);
        var unfinished = new ClanStructure(Guid.NewGuid(), 1, ClanId, 60, 60, Guid.NewGuid(), PlayerId, TimeSpan.FromHours(1), Now);

        Assert.Equal(1.12, await _bonus.StructureDefenceMultiplierAsync(built, Now, CancellationToken.None), 6);
        Assert.Equal(1, await _bonus.StructureDefenceMultiplierAsync(unfinished, Now, CancellationToken.None));
    }
}
