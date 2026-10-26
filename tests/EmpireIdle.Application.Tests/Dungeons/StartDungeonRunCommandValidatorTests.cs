using EmpireIdle.Application.Dungeons.Commands;
using EmpireIdle.Application.Dungeons.Validators;
using EmpireIdle.Domain.Services;
using EmpireIdle.Domain.Services.Config;

namespace EmpireIdle.Application.Tests.Dungeons;

/// <summary>Межа рівня данжу — з конфіга: піднятий MaxLevel не має відбиватися валідатором як сміття.</summary>
public class StartDungeonRunCommandValidatorTests
{
    private static StartDungeonRunCommandValidator Validator(int maxLevel) => new(new GameCatalog(new GameConfig
    {
        Buildings = [new BuildingConfig { Key = "townhall", IsMainBuilding = true }],
        Dungeons = new DungeonsConfig { MaxLevel = maxLevel }
    }));

    private static StartDungeonRunCommand Command(int level) => new(Guid.NewGuid(), "crypt", level, [Guid.NewGuid()]);

    [Theory]
    [InlineData(12, true)]
    [InlineData(13, false)]
    [InlineData(0, false)]
    public void Validate_ShouldBoundTheLevelByTheConfig(int level, bool valid)
        => Assert.Equal(valid, Validator(maxLevel: 12).Validate(Command(level)).IsValid);
}
