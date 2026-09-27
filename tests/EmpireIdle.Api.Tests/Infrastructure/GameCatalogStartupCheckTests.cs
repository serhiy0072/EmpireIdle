using EmpireIdle.API.Services;
using EmpireIdle.Domain.Services;
using EmpireIdle.Domain.Services.Config;
using Microsoft.Extensions.DependencyInjection;

namespace EmpireIdle.Api.Tests.Infrastructure;

/// <summary>
/// Битий конфіг має зупинити застосунок на старті, а не віддавати 500 на кожен запит
/// після того, як health-check уже пройдено.
/// </summary>
public class GameCatalogStartupCheckTests
{
    private static GameCatalogStartupCheck CheckFor(GameConfig config)
    {
        var services = new ServiceCollection()
            .AddSingleton(_ => new GameCatalog(config))
            .BuildServiceProvider();

        return new GameCatalogStartupCheck(services);
    }

    [Fact]
    public async Task StartAsync_ShouldPass_ForAValidConfig()
        => await CheckFor(new GameConfig
        {
            Buildings = [new BuildingConfig { Key = "townhall", IsMainBuilding = true }]
        }).StartAsync(CancellationToken.None);

    /// <summary>Узгодженість секцій (тут — дві головні будівлі) ловить лише GameConfigValidator у каталозі.</summary>
    [Fact]
    public async Task StartAsync_ShouldFail_WhenSectionsDisagree()
    {
        var config = new GameConfig
        {
            Buildings =
            [
                new BuildingConfig { Key = "townhall", IsMainBuilding = true },
                new BuildingConfig { Key = "keep", IsMainBuilding = true }
            ]
        };

        await Assert.ThrowsAsync<InvalidOperationException>(() => CheckFor(config).StartAsync(CancellationToken.None));
    }
}
