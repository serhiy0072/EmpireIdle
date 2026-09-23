using EmpireIdle.Domain.Services;
using EmpireIdle.Domain.Services.Config;
using EmpireIdle.TestKit;

namespace EmpireIdle.Domain.Tests.Services;

/// <summary>
/// Будівлі на плані села не налазять одна на одну. Інакше клієнт малює
/// силуети впереміш, і гравець не може влучити тапом у потрібну.
/// </summary>
public class BuildingLayoutValidationTests
{
    /// <summary>Ратуша, зала героїв і лікарня — позиції по порядку, решта без позиції.</summary>
    private static GameConfig Layout(params (double X, double Y)[] positions)
    {
        var config = new GameConfigBuilder().WithBuildings(TestKeys.Hall, TestKeys.Hospital).Build();

        for (var i = 0; i < positions.Length; i++)
            config.Buildings[i].Position = new BuildingPosition { X = positions[i].X, Y = positions[i].Y };

        return config;
    }

    [Fact]
    public void Validate_ShouldAccept_ASpacedLayout()
    {
        var exception = Record.Exception(() => GameConfigValidator.Validate(Layout((50, 50), (66, 50), (50, 66))));

        Assert.Null(exception);
    }

    [Fact]
    public void Validate_ShouldReject_BuildingsTooClose()
    {
        var exception = Assert.Throws<InvalidOperationException>(
            () => GameConfigValidator.Validate(Layout((50, 50), (58, 52))));

        Assert.Contains(TestKeys.Townhall, exception.Message);
        Assert.Contains(TestKeys.Hall, exception.Message);
    }

    /// <summary>Діагональні сусіди рівно на порозі не перетинаються: межа включна.</summary>
    [Fact]
    public void Validate_ShouldAccept_NeighboursExactlyAtTheSpacing()
    {
        var exception = Record.Exception(() => GameConfigValidator.Validate(Layout((50, 50), (62, 62))));

        Assert.Null(exception);
    }

    /// <summary>Будівля без позиції на мапі не малюється, тож і конфліктувати не може.</summary>
    [Fact]
    public void Validate_ShouldIgnoreBuildingsWithoutPosition()
    {
        var exception = Record.Exception(() => GameConfigValidator.Validate(Layout((50, 50))));

        Assert.Null(exception);
    }
}
