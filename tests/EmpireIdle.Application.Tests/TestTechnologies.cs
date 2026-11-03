using EmpireIdle.Application.Interfaces;
using EmpireIdle.Application.Research.Services;
using EmpireIdle.Domain.Entities;
using EmpireIdle.Domain.Services;
using EmpireIdle.Domain.Technologies;
using NSubstitute;

namespace EmpireIdle.Application.Tests;

/// <summary>Технології гравців для тестів, яким потрібні вивчені рівні (рішення 28.10.2026).</summary>
internal static class TestTechnologies
{
    private static readonly DateTime Now = new(2026, 11, 3, 12, 0, 0, DateTimeKind.Utc);

    /// <summary>Бонуси технологій із цього репозиторію; без репозиторію — жодних досліджень.</summary>
    public static PlayerTechnologies For(GameCatalog catalog, IPlayerResearchRepository? research = null)
        => new(research ?? Substitute.For<IPlayerResearchRepository>(),
            new PersonalTechnologies(catalog.Config.Technologies.Personal));

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
