using EmpireIdle.Domain.Enums;
using EmpireIdle.Domain.Services;
using EmpireIdle.Domain.Services.Config;
using EmpireIdle.TestKit;

namespace EmpireIdle.Domain.Tests.Services;

/// <summary>
/// Кланова територія й квести клану. Кожен тест ламає одну річ у валідному конфігу.
/// </summary>
public class ClanTerritoryValidationTests
{
    private const string ClanQuest = "clan_hunt";

    private static GameConfig Valid()
    {
        var config = new GameConfigBuilder().WithBuildings().Build();

        config.Quests =
        [
            new QuestConfig
            {
                Key = ClanQuest,
                DisplayName = "Clan hunt",
                Scope = QuestScope.Clan,
                ClanPoints = 500,
                Objectives = [new QuestObjectiveConfig { Type = "MonsterDefeated", Count = 100 }]
            },
            new QuestConfig
            {
                Key = "personal",
                DisplayName = "Personal",
                Objectives = [new QuestObjectiveConfig { Type = "MonsterDefeated", Count = 1 }]
            }
        ];

        config.Clan.Territory.SlotUnlocks =
        [
            new ClanSlotUnlockConfig { MinMembers = 20 },
            new ClanSlotUnlockConfig { QuestKey = ClanQuest }
        ];

        return config;
    }

    private static InvalidOperationException Rejects(Action<GameConfig> break_)
    {
        var config = Valid();
        break_(config);

        return Assert.Throws<InvalidOperationException>(() => GameConfigValidator.Validate(config));
    }

    [Fact]
    public void Validate_ShouldAcceptAValidTerritory()
        => Assert.Null(Record.Exception(() => GameConfigValidator.Validate(Valid())));

    [Fact]
    public void Validate_ShouldRejectAnUnlockWithBothConditions()
        => Rejects(c => c.Clan.Territory.SlotUnlocks[0].QuestKey = ClanQuest);

    [Fact]
    public void Validate_ShouldRejectAnUnlockWithoutAnyCondition()
        => Rejects(c => c.Clan.Territory.SlotUnlocks[0].MinMembers = null);

    /// <summary>Особистий квест виконує один гравець — слот клану він відкривати не може.</summary>
    [Fact]
    public void Validate_ShouldRejectAnUnlockByANonClanQuest()
        => Rejects(c => c.Clan.Territory.SlotUnlocks[1].QuestKey = "personal");

    [Fact]
    public void Validate_ShouldRejectMoreStartingSlotsThanStructures()
        => Rejects(c => c.Clan.Territory.StartingSlots = c.Clan.Territory.MaxStructures + 1);

    /// <summary>Нагорода клану — очки вкладу; особисту нікому з клану не видати.</summary>
    [Fact]
    public void Validate_ShouldRejectAClanQuestWithPersonalRewards()
        => Assert.Contains(ClanQuest, Rejects(c => c.Quests[0].Rewards.Add(new RewardConfig { Type = "Gems", Amount = 5 })).Message);

    /// <summary>Поріг — поточний стан одного гравця; для спільного прогресу клану він не має сенсу.</summary>
    [Fact]
    public void Validate_ShouldRejectAThresholdObjectiveInAClanQuest()
        => Rejects(c => c.Quests[0].Objectives[0].Mode = ObjectiveMode.Threshold);
}
