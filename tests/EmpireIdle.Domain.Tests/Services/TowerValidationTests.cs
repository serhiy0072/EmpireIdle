using EmpireIdle.Domain.Enums;
using EmpireIdle.Domain.Services;
using EmpireIdle.Domain.Services.Config;
using EmpireIdle.TestKit;

namespace EmpireIdle.Domain.Tests.Services;

/// <summary>«Тисячна вежа». Кожен тест ламає одну річ у валідному конфігу.</summary>
public class TowerValidationTests
{
    private static GameConfig Valid()
    {
        var config = new GameConfigBuilder().WithHeroes().Build();

        config.Tower = new TowerConfig
        {
            MaxFloor = 100,
            Anchors =
            [
                new TowerAnchorConfig { Floor = 1, HeroLevel = 1, UnitLevel = 1, SkillLevel = 1, StatMultiplier = 0.5 },
                new TowerAnchorConfig { Floor = 100, HeroLevel = 20, UnitLevel = 3, SkillLevel = 2, StatMultiplier = 1.5 }
            ],
            SetSteps = [new TowerSetStepConfig { FromFloor = 1, Multiplier = 1 }, new TowerSetStepConfig { FromFloor = 50, Multiplier = 1.05 }],
            Archetypes =
            [
                new TowerArchetypeConfig { Key = "duel", DisplayName = "Duel", Classes = ["warrior", "mage"] },
                new TowerArchetypeConfig { Key = "pair", DisplayName = "Pair", Classes = ["warrior", "warrior"] }
            ],
            BossArchetype = "duel",
            BossLane = 1,
            HeroRarities = [new TowerRarityStepConfig { FromFloor = 1, Rarities = [Rarity.Common, Rarity.Unique] }],
            GoldResource = TestKeys.Gold,
            Milestones =
            [
                new TowerMilestoneConfig { Every = 1, Stages = [Stage(1, new RewardConfig { Type = "Gems", Amount = 5 })] },
                new TowerMilestoneConfig { Every = 10, Stages = [Stage(1, new RewardConfig { Type = "Item", Key = TestKeys.EssenceT2, Amount = 1 })] }
            ]
        };

        return config;
    }

    private static TowerRewardStageConfig Stage(int fromFloor, params RewardConfig[] rewards)
        => new() { FromFloor = fromFloor, Options = [rewards.ToList()] };

    [Fact]
    public void ValidConfig_ShouldPass() => GameConfigValidator.Validate(Valid());

    /// <summary>Вежа без кривої не налаштована — перевіряти нічого.</summary>
    [Fact]
    public void TowerWithoutAnchors_ShouldPass()
    {
        var config = Valid();
        config.Tower = new TowerConfig();

        GameConfigValidator.Validate(config);
    }

    /// <summary>Крива, що не доходить до верху, лишила б верхні поверхи без ворога.</summary>
    [Fact]
    public void AnchorsShortOfTheTop_ShouldFail()
    {
        var config = Valid();
        config.Tower.MaxFloor = 200;

        Assert.Throws<InvalidOperationException>(() => GameConfigValidator.Validate(config));
    }

    /// <summary>Вище — не слабше: інакше наступний поверх виявився б легшим за пройдений.</summary>
    [Fact]
    public void WeakerEnemyHigherUp_ShouldFail()
    {
        var config = Valid();
        config.Tower.Anchors[1].StatMultiplier = 0.4;

        Assert.Throws<InvalidOperationException>(() => GameConfigValidator.Validate(config));
    }

    [Fact]
    public void EnemyAboveTheHeroLevelCap_ShouldFail()
    {
        var config = Valid();
        config.Tower.Anchors[1].HeroLevel = config.HeroSettings.MaxLevel + 1;

        Assert.Throws<InvalidOperationException>(() => GameConfigValidator.Validate(config));
    }

    [Fact]
    public void SetStepsNotFromTheFirstFloor_ShouldFail()
    {
        var config = Valid();
        config.Tower.SetSteps[0].FromFloor = 2;

        Assert.Throws<InvalidOperationException>(() => GameConfigValidator.Validate(config));
    }

    [Fact]
    public void ArchetypeWithUnknownClass_ShouldFail()
    {
        var config = Valid();
        config.Tower.Archetypes[1].Classes = ["warrior", "paladin"];

        Assert.Throws<InvalidOperationException>(() => GameConfigValidator.Validate(config));
    }

    /// <summary>Склад більший за загін не влазить у доріжки.</summary>
    [Fact]
    public void ArchetypeLargerThanTheSquad_ShouldFail()
    {
        var config = Valid();
        config.Tower.Archetypes[1].Classes = Enumerable.Repeat("warrior", config.RealtimeBattle.SquadSize + 1).ToList();

        Assert.Throws<InvalidOperationException>(() => GameConfigValidator.Validate(config));
    }

    [Fact]
    public void BossOnAnEmptyLane_ShouldFail()
    {
        var config = Valid();
        config.Tower.BossLane = 2;

        Assert.Throws<InvalidOperationException>(() => GameConfigValidator.Validate(config));
    }

    /// <summary>Магів-рідкісних немає: на цьому ступені склад «duel» лишився б без героя.</summary>
    [Fact]
    public void RarityWithoutATemplateForAClass_ShouldFail()
    {
        var config = Valid();
        config.Tower.HeroRarities.Add(new TowerRarityStepConfig { FromFloor = 50, Rarities = [Rarity.Common] });

        Assert.Throws<InvalidOperationException>(() => GameConfigValidator.Validate(config));
    }

    [Fact]
    public void UnknownGoldResource_ShouldFail()
    {
        var config = Valid();
        config.Tower.GoldResource = "silver";

        Assert.Throws<InvalidOperationException>(() => GameConfigValidator.Validate(config));
    }

    [Fact]
    public void MilestoneWithUnknownItem_ShouldFail()
    {
        var config = Valid();
        config.Tower.Milestones[1].Stages[0].Options[0][0].Key = "dragon_egg";

        Assert.Throws<InvalidOperationException>(() => GameConfigValidator.Validate(config));
    }

    /// <summary>Героя пошта вежі не видає — лише gems, ресурси й предмети.</summary>
    [Fact]
    public void MilestoneGivingAHero_ShouldFail()
    {
        var config = Valid();
        config.Tower.Milestones[1].Stages[0].Options[0][0] = new RewardConfig { Type = "Hero", Key = TestKeys.CommonHero, Amount = 1 };

        Assert.Throws<InvalidOperationException>(() => GameConfigValidator.Validate(config));
    }

    [Fact]
    public void MilestoneWithAnEmptyOption_ShouldFail()
    {
        var config = Valid();
        config.Tower.Milestones[0].Stages[0].Options.Add([]);

        Assert.Throws<InvalidOperationException>(() => GameConfigValidator.Validate(config));
    }

    [Fact]
    public void TowerAboveTheTownHallCeiling_ShouldFail()
    {
        var config = Valid();
        config.Tower.RequiresMainBuildingLevel = config.MaxBuildingLevel + 1;

        Assert.Throws<InvalidOperationException>(() => GameConfigValidator.Validate(config));
    }
}
