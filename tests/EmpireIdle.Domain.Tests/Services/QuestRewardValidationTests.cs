using EmpireIdle.Domain.Services;
using EmpireIdle.Domain.Services.Config;
using EmpireIdle.TestKit;

namespace EmpireIdle.Domain.Tests.Services;

/// <summary>Нагорода квесту посилається на ключ потрібного виду — перевірка на старті, не на видачі.</summary>
public class QuestRewardValidationTests
{
    private static GameConfig ConfigWith(RewardConfig reward, bool asTier = false)
    {
        var config = new GameConfigBuilder()
            .WithHeroes()
            .WithEquipment()
            .Build();

        var quest = new QuestConfig { Key = "test_quest" };

        if (asTier)
            quest.RewardTiers.Add(new RewardTierConfig { Rewards = [reward] });
        else
            quest.Rewards.Add(reward);

        config.Quests.Add(quest);

        return config;
    }

    private static RewardConfig Reward(string type, string key) => new() { Type = type, Key = key, Amount = 1 };

    [Theory]
    [InlineData("Equipment", TestKeys.Artifact)]
    [InlineData("Equipment", TestKeys.Weapon)]
    [InlineData("equipment", TestKeys.Artifact)]
    [InlineData("Item", TestKeys.EssenceT2)]
    public void Validate_ShouldAccept_RewardOfTheRightKind(string type, string key)
    {
        var exception = Record.Exception(() => GameConfigValidator.Validate(ConfigWith(Reward(type, key))));

        Assert.Null(exception);
    }

    [Theory]
    [InlineData("Item", TestKeys.Artifact)]
    [InlineData("item", TestKeys.Weapon)]
    [InlineData("Equipment", TestKeys.EssenceT2)]
    [InlineData("Equipment", "no_such_item")]
    public void Validate_ShouldReject_RewardOfTheWrongKind(string type, string key)
    {
        var exception = Assert.Throws<InvalidOperationException>(
            () => GameConfigValidator.Validate(ConfigWith(Reward(type, key))));

        Assert.Contains("test_quest", exception.Message);
    }

    [Fact]
    public void Validate_ShouldCheckServerQuestTiers()
        => Assert.Throws<InvalidOperationException>(
            () => GameConfigValidator.Validate(ConfigWith(Reward("Item", TestKeys.Artifact), asTier: true)));
}
