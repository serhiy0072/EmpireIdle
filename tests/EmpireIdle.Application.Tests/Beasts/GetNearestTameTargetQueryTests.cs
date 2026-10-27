using EmpireIdle.Application.Beasts.Queries;
using EmpireIdle.Application.Interfaces;
using EmpireIdle.Domain.Entities;
using EmpireIdle.Domain.Exceptions;
using EmpireIdle.Domain.Services;
using EmpireIdle.TestKit;
using NSubstitute;

namespace EmpireIdle.Application.Tests.Beasts;

/// <summary>«Знайти» у звіринці: найближчий до села монстр того типу, з якого виходить звір.</summary>
public class GetNearestTameTargetQueryTests
{
    private static readonly DateTime Now = new(2026, 10, 21, 12, 0, 0, DateTimeKind.Utc);
    private static readonly Guid PlayerId = Guid.NewGuid();

    private readonly IVillageRepository _villages = Substitute.For<IVillageRepository>();
    private readonly IMonsterRepository _monsters = Substitute.For<IMonsterRepository>();
    private readonly GameCatalog _catalog = new GameConfigBuilder().WithBeasts().BuildCatalog();
    private readonly Village _village = new(Guid.NewGuid(), PlayerId, "Test", [], 7, -3);

    public GetNearestTameTargetQueryTests()
        => _villages.GetByPlayerIdReadOnlyAsync(PlayerId, Arg.Any<CancellationToken>()).Returns(_village);

    private GetNearestTameTargetQueryHandler Handler() => new(_villages, _monsters, _catalog);

    /// <summary>Шукає монстра звіра навколо села й віддає клітину, куди слати марш.</summary>
    [Fact]
    public async Task Handle_ShouldReturnTheNearestMonsterOfTheBeastsType()
    {
        var monster = new Monster(Guid.NewGuid(), _village.ServerId, TestKeys.BeastMonster, 4, 9, -1, Now);
        _monsters.GetNearestAsync(_village.ServerId, TestKeys.BeastMonster, 7, -3, Arg.Any<CancellationToken>()).Returns(monster);

        var target = await Handler().Handle(new GetNearestTameTargetQuery(PlayerId, TestKeys.Beast), CancellationToken.None);

        Assert.NotNull(target);
        Assert.Equal((monster.Id, TestKeys.BeastMonster, 4, 9, -1), (target.MonsterId, target.MonsterKey, target.Level, target.X, target.Y));
    }

    /// <summary>Такого монстра у світі зараз немає — порожня відповідь, а не помилка.</summary>
    [Fact]
    public async Task Handle_ShouldReturnNull_WhenNoSuchMonsterLivesInTheWorld()
    {
        var target = await Handler().Handle(new GetNearestTameTargetQuery(PlayerId, TestKeys.Beast), CancellationToken.None);

        Assert.Null(target);
    }

    [Fact]
    public async Task Handle_ShouldThrowNotFound_ForAnUnknownBeast()
    {
        await Assert.ThrowsAsync<EntityNotFoundException>(() =>
            Handler().Handle(new GetNearestTameTargetQuery(PlayerId, "dragon"), CancellationToken.None));
    }
}
