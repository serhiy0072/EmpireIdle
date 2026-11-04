using EmpireIdle.Application.Interfaces;
using EmpireIdle.Application.Research.Services;
using EmpireIdle.Domain.Entities;
using EmpireIdle.Domain.Services;
using EmpireIdle.Domain.Services.Config;
using EmpireIdle.Domain.Technologies;
using EmpireIdle.TestKit;
using NSubstitute;

namespace EmpireIdle.Application.Tests;

/// <summary>Технології гравців для тестів, яким потрібні вивчені рівні (рішення 28.10.2026).</summary>
internal static class TestTechnologies
{
    private static readonly DateTime Now = new(2026, 11, 3, 12, 0, 0, DateTimeKind.Utc);

    /// <summary>Бонуси технологій із цього репозиторію; без репозиторію — жодних досліджень.</summary>
    public static PlayerTechnologies For(GameCatalog catalog, IPlayerResearchRepository? research = null,
        IClanResearchRepository? clanResearch = null)
        => For(catalog.Config.Technologies, research, clanResearch);

    /// <summary>
    /// Те саме з окремого дерева — для тестів із власним конфігом гри, де Лабораторії немає
    /// і валідатор каталогу дерева б не пропустив.
    /// </summary>
    public static PlayerTechnologies For(TechnologiesConfig technologies, IPlayerResearchRepository? research = null,
        IClanResearchRepository? clanResearch = null)
        => new(research ?? Substitute.For<IPlayerResearchRepository>(), new PersonalTechnologies(technologies.Personal),
            clanResearch ?? NoClan(), new ClanTechnologies(technologies.Clan));

    /// <summary>Гравець поза кланом.</summary>
    public static IClanResearchRepository NoClan()
    {
        var clanResearch = Substitute.For<IClanResearchRepository>();
        clanResearch.GetLevelsByMemberAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>())
            .Returns((IReadOnlyDictionary<string, int>?)null);

        return clanResearch;
    }

    /// <summary>
    /// Фіксація виробітку перед завершенням дослідження — з тими самими репозиторіями села й досліджень,
    /// що й у тесті; бустів крамниці й звірів немає.
    /// </summary>
    public static ResearchSettlement Settlement(GameCatalog catalog, IVillageRepository villages,
        IPlayerResearchRepository? research = null, IServerRepository? servers = null)
        => new(villages, servers ?? Substitute.For<IServerRepository>(),
            TestEffects.Resolver(Substitute.For<IActiveEffectRepository>(), catalog: catalog, research: research),
            new WorldGeometry(catalog.Config.Map), catalog, new PersonalTechnologies(catalog.Config.Technologies.Personal));

    /// <summary>Дерево фікстури TestKit, де attack_infantry ще й дає цей стат за рівень.</summary>
    public static TechnologiesConfig TreeWith(TechnologyStat stat, double perLevel)
        => TreeWith((stat, perLevel));

    /// <summary>Те саме з кількома статами на attack_infantry.</summary>
    public static TechnologiesConfig TreeWith(params (TechnologyStat Stat, double PerLevel)[] effects)
        => new GameConfigBuilder()
            .WithTechnologies(t => t.Personal.Nodes.Single(n => n.Key == "attack_infantry").Effects
                .AddRange(effects.Select(e => new TechnologyEffectConfig { Stat = e.Stat, PerLevel = e.PerLevel })))
            .Build()
            .Technologies;

    /// <summary>Гравець вивчив вузол тіру 1 до цього рівня — репозиторій віддає його для читання.</summary>
    public static void Researched(this IPlayerResearchRepository research, Guid playerId, string node, int level)
    {
        var record = new PlayerResearch(Guid.NewGuid(), playerId, 1, Now);
        var key = new TechnologyKey(1, node);

        for (var i = 0; i < level; i++)
        {
            record.Start(key, TimeSpan.FromMinutes(1), Now);
            record.CompleteDue(Now.AddMinutes(1));
        }

        research.GetByPlayerReadOnlyAsync(playerId, Arg.Any<CancellationToken>()).Returns(record);
    }
}
