using EmpireIdle.Domain.Enums;
using EmpireIdle.Domain.Services;
using EmpireIdle.Domain.Services.Config;
using EmpireIdle.TestKit;

namespace EmpireIdle.Domain.Tests.Services;

/// <summary>Телепорт ближнього переїзду без радіусу не переносив би нікуди (GDD §8.9).</summary>
public class TeleportConfigValidationTests
{
    private static GameConfig With(TeleportScope scope, int range)
    {
        var config = new GameConfigBuilder().WithBuildings().Build();
        config.Items.Add(new ItemConfig
        {
            Key = "teleport_x", DisplayName = "T", Description = "t", Type = "teleport", TeleportScope = scope, TeleportRange = range
        });

        return config;
    }

    [Fact]
    public void Validate_ShouldRejectANearbyTeleportWithoutARange()
        => Assert.Contains("teleport_x",
            Assert.Throws<InvalidOperationException>(() => GameConfigValidator.Validate(With(TeleportScope.Nearby, 0))).Message);

    [Theory]
    [InlineData(TeleportScope.Nearby, 100)]
    [InlineData(TeleportScope.Random, 0)]
    [InlineData(TeleportScope.ClanTerritory, 0)]
    public void Validate_ShouldAcceptAConfiguredTeleport(TeleportScope scope, int range)
        => Assert.Null(Record.Exception(() => GameConfigValidator.Validate(With(scope, range))));
}
