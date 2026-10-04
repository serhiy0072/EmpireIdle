using EmpireIdle.Application.Common.Services;
using EmpireIdle.Application.Interfaces;
using EmpireIdle.Domain.Services;
using EmpireIdle.TestKit;
using NSubstitute;

namespace EmpireIdle.Application.Tests;

/// <summary>
/// Резолвер ефектів для тестів, яким звірі не потрібні: звіринця немає,
/// лишаються бусти крамниці з переданого репозиторію.
/// </summary>
internal static class TestEffects
{
    public static EffectResolver Resolver(IActiveEffectRepository effects, IBeastPenRepository? pens = null,
        GameCatalog? catalog = null)
    {
        var resolved = catalog ?? new GameConfigBuilder().WithBuildings().BuildCatalog();

        return new EffectResolver(effects, pens ?? Substitute.For<IBeastPenRepository>(), resolved,
            new BeastProgression(resolved));
    }
}
