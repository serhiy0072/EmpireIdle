using EmpireIdle.Application.Common.Services;
using EmpireIdle.Application.Interfaces;
using EmpireIdle.Domain.Services;
using EmpireIdle.Domain.Services.Config;
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
        GameCatalog? catalog = null, IPlayerResearchRepository? research = null, IClanResearchRepository? clanResearch = null)
    {
        var resolved = catalog ?? new GameConfigBuilder().WithBuildings().BuildCatalog();

        return new EffectResolver(effects, pens ?? Substitute.For<IBeastPenRepository>(), resolved,
            new BeastProgression(resolved), TestTechnologies.For(resolved, research, clanResearch));
    }

    /// <summary>Множники виробітку без бустів і технологій; мапа — з фікстури TestKit, бо конфіги тестів її не описують.</summary>
    public static VillageProductionRates Rates(GameCatalog catalog)
        => new(Substitute.For<IServerRepository>(), Resolver(Substitute.For<IActiveEffectRepository>(), catalog: catalog),
            new WorldGeometry(new GameConfigBuilder()
                .WithMap(m => m.Geometry = new MapGeometryConfig
                {
                    RingBoundaries = [0.5], RingMultipliers = [1.0, 1.0], RingsAtFirstLevel = 1.0, FogMinShare = 1.0, FogMaxShare = 1.0
                })
                .Build().Map));
}
