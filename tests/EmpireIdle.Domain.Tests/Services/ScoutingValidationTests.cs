using EmpireIdle.Domain.Services;
using EmpireIdle.Domain.Services.Config;
using EmpireIdle.TestKit;

namespace EmpireIdle.Domain.Tests.Services;

/// <summary>Розвідка й завіса від неї. Кожен тест ламає одну річ у валідному конфігу.</summary>
public class ScoutingValidationTests
{
    private const string Tower = "scouttower";

    private static GameConfig Valid()
    {
        var config = new GameConfigBuilder().WithBuildings(Tower).WithUnits().Build();

        // Найшвидший юніт TestKit — кіннота зі швидкістю 10
        config.Combat.Scouting = new ScoutingConfig { RequiredBuilding = Tower, Speed = 30 };
        config.Items =
        [
            new ItemConfig { Key = "veil", DisplayName = "Veil", Description = "", Type = "scoutveil", DurationHours = 24 }
        ];

        return config;
    }

    [Fact]
    public void ValidConfig_ShouldPass() => GameConfigValidator.Validate(Valid());

    /// <summary>Без будівлі розвідки у світі немає — і перевіряти нічого.</summary>
    [Fact]
    public void DisabledScouting_ShouldPass()
    {
        var config = Valid();
        config.Combat.Scouting.RequiredBuilding = null;
        config.Combat.Scouting.Speed = 1;

        GameConfigValidator.Validate(config);
    }

    [Fact]
    public void UnknownTower_ShouldFail()
    {
        var config = Valid();
        config.Combat.Scouting.RequiredBuilding = "watchtower";

        Assert.Throws<InvalidOperationException>(() => GameConfigValidator.Validate(config));
    }

    /// <summary>Розвідка, що не обганяє кінноту, приходить разом із нападом — сенсу в ній немає.</summary>
    [Fact]
    public void ScoutsNotFasterThanTheFastestUnit_ShouldFail()
    {
        var config = Valid();
        config.Combat.Scouting.Speed = 10;

        Assert.Throws<InvalidOperationException>(() => GameConfigValidator.Validate(config));
    }

    [Fact]
    public void VeilWithoutDuration_ShouldFail()
    {
        var config = Valid();
        config.Items[0].DurationHours = 0;

        Assert.Throws<InvalidOperationException>(() => GameConfigValidator.Validate(config));
    }
}
