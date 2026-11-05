using EmpireIdle.Domain.Services;
using EmpireIdle.Domain.Services.Config;
using EmpireIdle.TestKit;

namespace EmpireIdle.Domain.Tests.Services;

/// <summary>Черга ресурсів підношень і їхні ціни мусять називати ті самі ресурси.</summary>
public class ClanOfferingRotationValidationTests
{
    private static GameConfig With(Action<ClanOfferingConfig> tune)
        => new GameConfigBuilder().WithTechnologies(t => tune(t.Clan.Offering)).Build();

    [Fact]
    public void Validate_ShouldAccept_ARotationOfExactlyThePricedResources()
        => Assert.Null(Record.Exception(() => GameConfigValidator.Validate(With(_ => { }))));

    [Fact]
    public void Validate_ShouldRefuse_ARotatedResourceWithoutAPrice()
    {
        var config = With(offering => offering.ResourceRotation.Add(TestKeys.Iron));

        var error = Assert.Throws<InvalidOperationException>(() => GameConfigValidator.Validate(config));

        Assert.Contains("ResourceRotation", error.Message);
    }

    [Fact]
    public void Validate_ShouldRefuse_APriceThatTheRotationNeverReaches()
    {
        var config = With(offering => offering.ResourceRotation.Remove(TestKeys.Wood));

        var error = Assert.Throws<InvalidOperationException>(() => GameConfigValidator.Validate(config));

        Assert.Contains("ResourceRotation", error.Message);
    }

    [Fact]
    public void Validate_ShouldRefuse_AnEmptyRotation()
    {
        var config = With(offering => offering.ResourceRotation.Clear());

        var error = Assert.Throws<InvalidOperationException>(() => GameConfigValidator.Validate(config));

        Assert.Contains("ResourceRotation", error.Message);
    }
}
