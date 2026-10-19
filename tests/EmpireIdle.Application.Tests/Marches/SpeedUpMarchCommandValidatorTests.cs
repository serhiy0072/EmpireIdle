using EmpireIdle.Application.Marches.Commands;
using EmpireIdle.Application.Marches.Validators;
using EmpireIdle.Domain.Services;
using EmpireIdle.Domain.Services.Config;
using EmpireIdle.TestKit;

namespace EmpireIdle.Application.Tests.Marches;

/// <summary>Прискорення маршу береться лише з варіантів конфіга — довільний відсоток не проходить.</summary>
public class SpeedUpMarchCommandValidatorTests
{
    private readonly SpeedUpMarchCommandValidator _validator;

    public SpeedUpMarchCommandValidatorTests()
    {
        var config = new GameConfigBuilder().WithBuildings().Build();
        config.Monetization.MarchSpeedUps = [new MarchSpeedUpConfig { Percent = 50, Gems = 200 }, new MarchSpeedUpConfig { Percent = 90, Gems = 800 }];
        _validator = new SpeedUpMarchCommandValidator(new GameCatalog(config));
    }

    [Theory]
    [InlineData(50, true)]
    [InlineData(90, true)]
    [InlineData(75, false)]
    [InlineData(100, false)]
    [InlineData(0, false)]
    public void Validate_ShouldTakeOnlyConfiguredOptions(int percent, bool valid)
        => Assert.Equal(valid, _validator.Validate(new SpeedUpMarchCommand(Guid.NewGuid(), Guid.NewGuid(), percent)).IsValid);
}
