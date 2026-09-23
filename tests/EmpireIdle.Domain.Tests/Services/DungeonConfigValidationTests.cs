using EmpireIdle.Domain.Services;
using EmpireIdle.Domain.Services.Config;
using EmpireIdle.TestKit;

namespace EmpireIdle.Domain.Tests.Services;

/// <summary>
/// Данжі й родини наборів артефактів. Кожен тест ламає одну річ
/// у валідному конфігу з одним данжем.
/// </summary>
public class DungeonConfigValidationTests
{
    private static GameConfig Valid() => new GameConfigBuilder().WithDungeons().Build();

    private static InvalidOperationException Rejects(Action<GameConfig> break_)
    {
        var config = Valid();
        break_(config);

        return Assert.Throws<InvalidOperationException>(() => GameConfigValidator.Validate(config));
    }

    [Fact]
    public void Validate_ShouldAcceptTheDungeonConfig()
        => Assert.Null(Record.Exception(() => GameConfigValidator.Validate(Valid())));

    /// <summary>Данж на 32 при стелі ратуші 30 — саме та помилка, що дійшла до гравця.</summary>
    [Fact]
    public void Validate_ShouldRejectADungeonUnlockedAboveTheTownHallCeiling()
    {
        var error = Rejects(c =>
        {
            c.Map.MaxServerLevel = 3;
            c.BuildingLevelsPerTier = 10;
            c.Dungeons.Dungeons.Single().RequiresMainBuildingLevel = 32;
        });

        Assert.Contains($"dungeon {TestKeys.Dungeon} (32)", error.Message);
    }

    /// <summary>Кілька данжів на одному порозі — дозволено: це рівень данжів із парою наборів.</summary>
    [Fact]
    public void Validate_ShouldAcceptTwoDungeonsOnTheSameThreshold()
    {
        var config = Valid();
        var first = config.Dungeons.Dungeons.Single();

        config.Dungeons.Dungeons.Add(new DungeonConfig
        {
            Key = "second_pit",
            DisplayName = "Друга яма",
            ArtifactSetKey = first.ArtifactSetKey,
            RequiresMainBuildingLevel = first.RequiresMainBuildingLevel,
            Waves = first.Waves,
            Boss = first.Boss,
            Reward = first.Reward
        });

        Assert.Null(Record.Exception(() => GameConfigValidator.Validate(config)));
    }

    /// <summary>Набір данжу без опису родини ролився б без рівня — мовчки слабшим.</summary>
    [Fact]
    public void Validate_ShouldRejectADungeonSetWithoutAFamily()
        => Assert.Contains(TestKeys.DungeonSetKey, Rejects(c => c.Equipment.ArtifactSets.Clear()).Message);

    [Fact]
    public void Validate_ShouldRejectDuplicateFamilies()
        => Rejects(c => c.Equipment.ArtifactSets.Add(new ArtifactSetConfig { Key = TestKeys.DungeonSetKey }));

    /// <summary>Рівень поза списком множників — помилка конфіга, а не «останній відомий».</summary>
    [Theory]
    [InlineData(0)]
    [InlineData(3)]
    public void Validate_ShouldRejectATierOutsideTheMultipliers(int tier)
        => Rejects(c =>
        {
            c.Equipment.ArtifactTierMultipliers = [1.0, 1.5];
            c.Equipment.ArtifactSets.Single().Tier = tier;
        });

    /// <summary>Характер у стат, якого немає в пулі, тихо нічого б не робив.</summary>
    [Fact]
    public void Validate_ShouldRejectAFocusStatOutsideThePool()
        => Assert.Contains("Luck", Rejects(c => c.Equipment.ArtifactSets.Single().FocusStats = ["Luck"]).Message);
}
