using EmpireIdle.Application.Battles.Services;
using EmpireIdle.Application.Interfaces;
using EmpireIdle.Application.Rewards;
using EmpireIdle.Application.Rewards.Contracts;
using EmpireIdle.Application.Tower.Commands;
using EmpireIdle.Domain.Battles;
using EmpireIdle.Domain.Combat;
using EmpireIdle.Domain.Entities;
using EmpireIdle.Domain.Enums;
using EmpireIdle.Domain.Exceptions;
using EmpireIdle.Domain.Services;
using EmpireIdle.Domain.Services.Config;
using EmpireIdle.Domain.Tower;
using EmpireIdle.TestKit;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Time.Testing;
using NSubstitute;

namespace EmpireIdle.Application.Tests.Tower;

/// <summary>
/// Штурм поверху: ратуша відкриває вежу, перемога закриває поверх і видає нагороду одним збереженням,
/// поразка не змінює нічого. Вежа на 3 поверхи; сила ворога регулюється множником кривої.
/// </summary>
public class StormTowerFloorCommandTests
{
    private static readonly Guid PlayerId = Guid.NewGuid();

    private readonly GameConfig _config;
    private readonly ITowerRepository _tower = Substitute.For<ITowerRepository>();
    private readonly IVillageRepository _villages = Substitute.For<IVillageRepository>();
    private readonly IHeroRepository _heroes = Substitute.For<IHeroRepository>().ForwardHeroLookups();
    private readonly IInventoryRepository _inventory = Substitute.For<IInventoryRepository>();
    private readonly IGarrisonRepository _garrisons = Substitute.For<IGarrisonRepository>();
    private readonly IUnitOfWork _unitOfWork = Substitute.For<IUnitOfWork>();
    private readonly IRewardGranter _gold = Granter("Resource");
    private readonly IRewardGranter _gems = Granter("Gems");

    public StormTowerFloorCommandTests()
    {
        _config = new GameConfigBuilder()
            .WithUnits()
            .WithHeroes(h =>
            {
                h.RoleUnits = new() { ["warrior"] = TestKeys.Infantry, ["knight"] = TestKeys.Cavalry, ["archer"] = TestKeys.Archer, ["mage"] = TestKeys.Archer };
                h.ConvoySize = 100;
                h.ConvoysByLevel = [new ConvoyStepConfig { Level = 1, Convoys = 1 }];
            })
            .Build();

        foreach (var hero in _config.Heroes)
            hero.BaseStats["Health"] = 500;

        var battle = _config.RealtimeBattle;
        battle.FrontLineClasses = ["warrior"];
        battle.HeroAttackIntervals = new() { ["warrior"] = 1.25, ["knight"] = 1.0, ["archer"] = 1.3, ["mage"] = 1.3 };
        battle.UnitAttackIntervals = new() { [TestKeys.Infantry] = 1.25, [TestKeys.Cavalry] = 1.0, [TestKeys.Archer] = 1.4 };

        _config.Tower = new TowerConfig
        {
            RequiresMainBuildingLevel = 3,
            MaxFloor = 3,
            Anchors =
            [
                new TowerAnchorConfig { Floor = 1, HeroLevel = 1, UnitLevel = 1, StatMultiplier = 0.01 },
                new TowerAnchorConfig { Floor = 3, HeroLevel = 1, UnitLevel = 1, StatMultiplier = 0.01 }
            ],
            SetSteps = [new TowerSetStepConfig { FromFloor = 1, Multiplier = 1 }],
            Archetypes = [new TowerArchetypeConfig { Key = "lone", DisplayName = "Lone", Classes = ["warrior"] }],
            BossEvery = 10,
            BossArchetype = "lone",
            BossLane = 0,
            HeroRarities = [new TowerRarityStepConfig { FromFloor = 1, Rarities = [Rarity.Common] }],
            GoldResource = TestKeys.Gold,
            Milestones = [new TowerMilestoneConfig { Every = 1, Stages = [new TowerRewardStageConfig { Options = [[new RewardConfig { Type = "Gems", Amount = 5 }]] }] }]
        };

        _villages.GetByPlayerIdReadOnlyAsync(PlayerId, Arg.Any<CancellationToken>()).Returns(Entities.VillageWithTownhall(townhallLevel: 3));
        _inventory.GetEquippedAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>()).Returns([]);
    }

    private static IRewardGranter Granter(string type)
    {
        var granter = Substitute.For<IRewardGranter>();
        granter.RewardType.Returns(type);
        return granter;
    }

    private StormTowerFloorCommandHandler Handler()
    {
        var catalog = new GameCatalog(_config);
        var progression = new HeroProgression(_config.HeroSettings);
        var effects = TestEffects.Resolver(Substitute.For<IActiveEffectRepository>(), catalog: catalog);
        var serverContext = Substitute.For<IServerContext>();
        serverContext.ServerId.Returns(1);

        var squads = new BattleSquadFactory(_heroes, _inventory, _villages, _garrisons, catalog, new HeroStats(progression, catalog),
            new HeroSkills(_config.HeroSettings), new HeroCombatModifiers(catalog), effects);

        return new StormTowerFloorCommandHandler(_tower, _villages, squads, new TowerFloors(catalog), new TowerRewards(_config.Tower),
            new RealtimeBattleEngine(_config.RealtimeBattle), new RewardDispatcher([_gold, _gems]), catalog, serverContext, _unitOfWork,
            new FakeTimeProvider(Entities.Now), NullLogger<StormTowerFloorCommandHandler>.Instance);
    }

    private StormTowerFloorCommand Storm(int floor)
    {
        var hero = Entities.Hero(TestKeys.CommonHero, PlayerId, level: 30);
        _heroes.GetByIdAsync(hero.Id, Arg.Any<CancellationToken>()).Returns(hero);

        return new StormTowerFloorCommand(PlayerId, floor, [hero.Id]);
    }

    /// <summary>Перший поверх уперше: новий прогрес, золото й gems, одне збереження.</summary>
    [Fact]
    public async Task Handle_ShouldClearTheFloor_AndGrantItsReward_OnVictory()
    {
        var result = await Handler().Handle(Storm(1), CancellationToken.None);

        Assert.True(result.Victory);
        Assert.Equal(BattleSide.Attacker, result.Battle.Winner);
        Assert.Equal(["Resource:210", "Gems:5"], result.Rewards.Select(r => $"{r.Type}:{r.Amount}"));

        await _tower.Received(1).AddProgressAsync(Arg.Is<TowerProgress>(p => p.HighestFloor == 1 && p.PlayerId == PlayerId && p.ServerId == 1),
            Arg.Any<CancellationToken>());
        await _gold.Received(1).GrantAsync(Arg.Is<RewardContext>(c => c.Reward.Amount == 210 && c.Reference == "tower:1"), Arg.Any<CancellationToken>());
        await _gems.Received(1).GrantAsync(Arg.Any<RewardContext>(), Arg.Any<CancellationToken>());
        await _unitOfWork.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    /// <summary>Наступний поверх іде по наявному прогресу — новий рядок не створюється.</summary>
    [Fact]
    public async Task Handle_ShouldAdvanceExistingProgress()
    {
        var progress = new TowerProgress(Guid.NewGuid(), PlayerId, serverId: 1);
        progress.Clear(1, maxFloor: 3, Entities.Now);
        _tower.GetProgressAsync(PlayerId, Arg.Any<CancellationToken>()).Returns(progress);

        await Handler().Handle(Storm(2), CancellationToken.None);

        Assert.Equal(2, progress.HighestFloor);
        await _tower.DidNotReceiveWithAnyArgs().AddProgressAsync(default!, default);
    }

    /// <summary>Програш безплатний: ні прогресу, ні нагороди, ні збереження.</summary>
    [Fact]
    public async Task Handle_ShouldChangeNothing_OnDefeat()
    {
        _config.Tower.Anchors.ForEach(a => a.StatMultiplier = 1000);

        var result = await Handler().Handle(Storm(1), CancellationToken.None);

        Assert.False(result.Victory);
        Assert.Empty(result.Rewards);
        await _tower.DidNotReceiveWithAnyArgs().AddProgressAsync(default!, default);
        await _gold.DidNotReceiveWithAnyArgs().GrantAsync(default!, default);
        await _unitOfWork.DidNotReceiveWithAnyArgs().SaveChangesAsync(default);
    }

    /// <summary>Пройдений поверх уже закритий — бій навіть не рахується.</summary>
    [Fact]
    public async Task Handle_ShouldRefuseAClosedFloor()
    {
        var refusal = await Assert.ThrowsAsync<RequirementNotMetException>(() => Handler().Handle(Storm(2), CancellationToken.None));

        Assert.Equal(RefusalReasons.TowerFloorClosed.Key, refusal.Reason);
    }

    [Fact]
    public async Task Handle_ShouldRefuse_BelowTheTownHallLevel()
    {
        _villages.GetByPlayerIdReadOnlyAsync(PlayerId, Arg.Any<CancellationToken>()).Returns(Entities.VillageWithTownhall(townhallLevel: 2));

        var refusal = await Assert.ThrowsAsync<RequirementNotMetException>(() => Handler().Handle(Storm(1), CancellationToken.None));

        Assert.Equal(RefusalReasons.TowerTownHallRequired.Key, refusal.Reason);
    }

    /// <summary>Той самий загін на тому самому поверсі — той самий бій: ретраї не «ролять кубик».</summary>
    [Fact]
    public async Task Handle_ShouldReplayTheSameBattle_ForTheSameSquad()
    {
        var command = Storm(1);

        var first = await Handler().Handle(command, CancellationToken.None);
        var second = await Handler().Handle(command, CancellationToken.None);

        Assert.Equal(first.Battle.Events, second.Battle.Events);
    }
}
