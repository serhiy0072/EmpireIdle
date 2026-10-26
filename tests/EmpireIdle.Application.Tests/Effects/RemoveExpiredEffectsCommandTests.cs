using EmpireIdle.Application.Effects.Commands;
using EmpireIdle.Application.Interfaces;
using EmpireIdle.Domain.Services;
using EmpireIdle.Domain.Services.Config;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Time.Testing;
using NSubstitute;

namespace EmpireIdle.Application.Tests.Effects;

/// <summary>
/// Буст, що щойно скінчився, ще потрібен наступному збору: його вікно множить виробіток
/// за останній проміжок. Прибирання бере лише ті, що скінчились понад запас тому.
/// </summary>
public class RemoveExpiredEffectsCommandTests
{
    private static readonly DateTime Now = new(2026, 10, 26, 12, 0, 0, DateTimeKind.Utc);

    [Fact]
    public async Task Handle_ShouldKeepEffectsWithinTheGrace()
    {
        var effects = Substitute.For<IActiveEffectRepository>();
        var catalog = new GameCatalog(new GameConfig
        {
            Buildings = [new BuildingConfig { Key = "townhall", IsMainBuilding = true }],
            Retention = new RetentionConfig { ExpiredEffectHours = 24 }
        });

        await new RemoveExpiredEffectsCommandHandler(effects, catalog, new FakeTimeProvider(Now),
                NullLogger<RemoveExpiredEffectsCommandHandler>.Instance)
            .Handle(new RemoveExpiredEffectsCommand(), CancellationToken.None);

        await effects.Received(1).RemoveExpiredAsync(Now.AddHours(-24), Arg.Any<CancellationToken>());
    }
}
