using EmpireIdle.Domain.Enums;
using EmpireIdle.Domain.Services;
using EmpireIdle.Domain.Services.Config;
using EmpireIdle.TestKit;

namespace EmpireIdle.Domain.Tests.Services;

/// <summary>
/// Стати артефактів (GDD §9.12): база, бонуси заточки, пули слотів і шанси.
/// Кожен тест ламає одну річ у валідному конфігу спорядження.
/// </summary>
public class ArtifactStatsValidationTests
{
    private static GameConfig Valid() => new GameConfigBuilder()
        .WithHeroes()
        .WithEquipment(e => e.ArtifactClassMultipliers["warrior"] = new ArtifactClassMultiplierConfig { Attack = 1.0, Defense = 1.2 })
        .Build();

    private static InvalidOperationException Rejects(Action<GameConfig> break_)
    {
        var config = Valid();
        break_(config);

        return Assert.Throws<InvalidOperationException>(() => GameConfigValidator.Validate(config));
    }

    private static ArtifactSlotConfig Slot(GameConfig config, string key)
        => config.Equipment.ArtifactSlots.Single(s => s.Key == key);

    [Fact]
    public void Validate_ShouldAcceptTheArtifactStats()
        => Assert.Null(Record.Exception(() => GameConfigValidator.Validate(Valid())));

    /// <summary>Рідкість без бази дала б артефакт без жодного стату.</summary>
    [Fact]
    public void Validate_ShouldRejectARarityWithoutABase()
        => Assert.Contains("Common", Rejects(c => c.Equipment.ArtifactBase.Remove(Rarity.Common)).Message);

    [Fact]
    public void Validate_ShouldRejectAClassMultiplierForAnUnknownClass()
        => Assert.Contains("paladin", Rejects(c => c.Equipment.ArtifactClassMultipliers["paladin"] = new()).Message);

    /// <summary>Ступенів менше, ніж шансів, — і кидок четвертого ступеня вийде за межі списку.</summary>
    [Fact]
    public void Validate_ShouldRejectABonusWithTooFewSteps()
        => Assert.Contains("CritChance",
            Rejects(c => c.Equipment.FindArtifactBonus("CritChance")!.Steps = [1, 2, 3]).Message);

    [Fact]
    public void Validate_ShouldRejectFallingSteps()
        => Assert.Contains("CritChance",
            Rejects(c => c.Equipment.FindArtifactBonus("CritChance")!.Steps = [4, 3, 2, 1]).Message);

    [Fact]
    public void Validate_ShouldRejectDuplicateBonuses()
        => Rejects(c => c.Equipment.ArtifactBonuses.Add(new ArtifactBonusConfig { Stat = "Lifesteal", Steps = [1, 2, 3, 4] }));

    [Fact]
    public void Validate_ShouldRejectASlotWithOneFixedBonus()
        => Assert.Contains("necklace", Rejects(c => Slot(c, "necklace").FixedBonuses = ["Attack"]).Message);

    /// <summary>Сталий бонус підсилює базу предмета — крит підсилювати нічого.</summary>
    [Fact]
    public void Validate_ShouldRejectAFixedBonusOutsideTheBaseStats()
        => Assert.Contains("CritChance",
            Rejects(c => Slot(c, "necklace").FixedBonuses = ["Attack", "CritChance"]).Message);

    /// <summary>Другий випадковий бонус без повтору потребує бодай двох статів у пулі.</summary>
    [Fact]
    public void Validate_ShouldRejectAPoolOfOne()
        => Assert.Contains("belt",
            Rejects(c => Slot(c, "belt").RandomBonuses.RemoveAll(e => e.Stat != "HealthPercent")).Message);

    [Fact]
    public void Validate_ShouldRejectAPoolStatWithoutABonus()
        => Assert.Contains("Luck",
            Rejects(c => Slot(c, "ring").RandomBonuses.Add(new ArtifactPoolEntryConfig { Stat = "Luck", Weight = 1 })).Message);

    /// <summary>Базовий стат у пулі героя подвоїв би базу замість бонусу героя.</summary>
    [Fact]
    public void Validate_ShouldRejectABaseStatInTheRandomPool()
        => Assert.Contains("Attack",
            Rejects(c => Slot(c, "ring").RandomBonuses.Add(new ArtifactPoolEntryConfig { Stat = "Attack", Weight = 1 })).Message);

    [Fact]
    public void Validate_ShouldRejectAZeroWeight()
        => Assert.Contains("CritChance", Rejects(c => Slot(c, "necklace").RandomBonuses[0].Weight = 0).Message);

    /// <summary>Шансів менше за стелю — спроба на останні рангові кроки не мала б шансу.</summary>
    [Fact]
    public void Validate_ShouldRejectMasteryChancesShorterThanTheCeiling()
        => Assert.Contains("MasterySuccessChances",
            Rejects(c => c.Equipment.MasterySuccessChances.RemoveAt(19)).Message);
}
