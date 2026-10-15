using EmpireIdle.Domain.Services;
using EmpireIdle.Domain.Services.Config;
using EmpireIdle.TestKit;

namespace EmpireIdle.Domain.Tests.Services;

/// <summary>
/// Пороги бонусів набору (рішення 15.10.2026): 2 і 4 предмети родини, по одному бонусу на поріг.
/// Кожен тест ламає одну річ у валідному конфігу спорядження.
/// </summary>
public class SetBonusValidationTests
{
    private static GameConfig Valid() => new GameConfigBuilder().WithHeroes().WithEquipment().Build();

    private static InvalidOperationException Rejects(Action<GameConfig> break_)
    {
        var config = Valid();
        break_(config);

        return Assert.Throws<InvalidOperationException>(() => GameConfigValidator.Validate(config));
    }

    private static SetBonusConfig Bonus(int pieces)
        => new() { SetKey = TestKeys.SetKey, RequiredPieces = pieces, Stats = new Dictionary<string, double> { ["Attack"] = 5 } };

    [Fact]
    public void Validate_ShouldAcceptBothThresholdsOfOneSet()
    {
        var config = Valid();
        config.Equipment.SetBonuses.Add(Bonus(2));

        Assert.Null(Record.Exception(() => GameConfigValidator.Validate(config)));
    }

    [Theory]
    [InlineData(1)]
    [InlineData(3)]
    public void Validate_ShouldRejectAThresholdOtherThanTwoOrFour(int pieces)
        => Assert.Contains("thresholds are 2 or 4", Rejects(c => c.Equipment.SetBonuses[0].RequiredPieces = pieces).Message);

    /// <summary>Два бонуси на один поріг — незрозуміло, який діє.</summary>
    [Fact]
    public void Validate_ShouldRejectTwoBonusesForTheSameThreshold()
        => Assert.Contains("duplicate", Rejects(c => c.Equipment.SetBonuses.Add(Bonus(4))).Message);

    /// <summary>Стат, якого не рахує жоден бій, тихо нічого б не давав.</summary>
    [Fact]
    public void Validate_ShouldRejectAnUnknownStat()
        => Assert.Contains("AttackSpeed", Rejects(c => c.Equipment.SetBonuses[0].Stats["AttackSpeed"] = 5).Message);

    /// <summary>Поріг може дати будь-який бонус заточки й цілі ходи перезарядки.</summary>
    [Fact]
    public void Validate_ShouldAcceptMasteryStatsAndCooldownTurns()
    {
        var config = Valid();
        config.Equipment.SetBonuses[0].Stats = new() { ["Ward"] = 9, ["TroopsAttack"] = 5, ["CooldownTurns"] = 1, ["HealthPercent"] = 6.5 };

        Assert.Null(Record.Exception(() => GameConfigValidator.Validate(config)));
    }

    [Theory]
    [InlineData(0.5)]
    [InlineData(0)]
    public void Validate_ShouldRejectAFractionalOrZeroCooldownCut(double turns)
        => Assert.Contains("whole number of turns", Rejects(c => c.Equipment.SetBonuses[0].Stats["CooldownTurns"] = turns).Message);

    /// <summary>
    /// Слоти рахуються по всій родині: два предмети звичайної рідкості й два унікальної
    /// в різних слотах разом дають 4 і закривають поріг 4 для кожної з рідкостей.
    /// </summary>
    [Fact]
    public void Validate_ShouldCountSlotsAcrossTheWholeFamily()
    {
        var config = Valid();
        config.Equipment.ArtifactSets.Add(new ArtifactSetConfig { Key = "amber", DisplayName = "Набір Бурштину" });
        config.Items.AddRange(
        [
            Artifact("amber_necklace_common", "amber_common", "necklace"),
            Artifact("amber_ring_common", "amber_common", "ring"),
            Artifact("amber_crown_unique", "amber_unique", "crown"),
            Artifact("amber_belt_unique", "amber_unique", "belt")
        ]);
        config.Equipment.SetBonuses.Add(new SetBonusConfig { SetKey = "amber_common", RequiredPieces = 4, Stats = new() { ["Attack"] = 5 } });

        Assert.Null(Record.Exception(() => GameConfigValidator.Validate(config)));
    }

    private static ItemConfig Artifact(string key, string setKey, string slot)
    {
        var template = Valid().Items.Single(i => i.Key == TestKeys.Artifact);

        return new ItemConfig
        {
            Key = key,
            Type = template.Type,
            DisplayName = key,
            Description = template.Description,
            Slot = template.Slot,
            ArtifactSlot = slot,
            SetKey = setKey
        };
    }
}
