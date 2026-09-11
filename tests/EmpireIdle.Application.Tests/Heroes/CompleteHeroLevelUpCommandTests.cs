using EmpireIdle.Application.Heroes.Commands;
using EmpireIdle.Application.Interfaces;
using EmpireIdle.Domain.Entities;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Time.Testing;
using NSubstitute;

namespace EmpireIdle.Application.Tests.Heroes;

/// <summary>
/// Одиниця роботи сканера таймерів. Найдорожча помилка тут — замовлення,
/// яке не прибирається: воно назавжди займає єдину чергу гравця.
/// </summary>
public class CompleteHeroLevelUpCommandTests
{
    private static readonly DateTime Now = new(2026, 6, 1, 12, 0, 0, DateTimeKind.Utc);
    private static readonly Guid PlayerId = Guid.NewGuid();
    private const int ServerId = 1;

    private readonly IHeroRepository _heroes = Substitute.For<IHeroRepository>();
    private readonly IUnitOfWork _unitOfWork = Substitute.For<IUnitOfWork>();

    private CompleteHeroLevelUpCommandHandler Handler() => new(
        _heroes, _unitOfWork, new FakeTimeProvider(Now),
        NullLogger<CompleteHeroLevelUpCommandHandler>.Instance);

    private (Hero Hero, HeroLevelOrder Order) GivenDueOrder(DateTime? completesAt = null)
    {
        var hero = new Hero(Guid.NewGuid(), PlayerId, ServerId, "warrior_bran", Now);

        var order = new HeroLevelOrder(Guid.NewGuid(), hero.Id, PlayerId, ServerId,
            targetLevel: 2, completesAt ?? Now.AddMinutes(-1));

        _heroes.GetOrderByIdAsync(order.Id, Arg.Any<CancellationToken>()).Returns(order);
        _heroes.GetByIdAsync(hero.Id, Arg.Any<CancellationToken>()).Returns(hero);

        return (hero, order);
    }

    [Fact]
    public async Task Handle_ShouldRaiseLevelAndRemoveTheOrder()
    {
        var (hero, order) = GivenDueOrder();

        await Handler().Handle(new CompleteHeroLevelUpCommand(order.Id), CancellationToken.None);

        Assert.Equal(2, hero.Level);
        _heroes.Received(1).RemoveOrder(order);
    }

    /// <summary>
    /// Не дозріле замовлення сканер пропускає мовчки: воно потрапить
    /// у наступний прогін.
    /// </summary>
    [Fact]
    public async Task Handle_ShouldDoNothing_BeforeTheDeadline()
    {
        var (hero, order) = GivenDueOrder(completesAt: Now.AddMinutes(5));

        await Handler().Handle(new CompleteHeroLevelUpCommand(order.Id), CancellationToken.None);

        Assert.Equal(1, hero.Level);
        _heroes.DidNotReceive().RemoveOrder(Arg.Any<HeroLevelOrder>());
    }

    /// <summary>Зникле замовлення не валить прогін сканера.</summary>
    [Fact]
    public async Task Handle_ShouldDoNothing_WhenTheOrderIsGone()
    {
        var id = Guid.NewGuid();
        _heroes.GetOrderByIdAsync(id, Arg.Any<CancellationToken>()).Returns((HeroLevelOrder?)null);

        await Handler().Handle(new CompleteHeroLevelUpCommand(id), CancellationToken.None);

        await _unitOfWork.DidNotReceive().SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    /// <summary>
    /// Героя могли видалити між вибіркою й обробкою. Замовлення прибирається
    /// однаково — інакше воно назавжди займе єдину чергу гравця.
    /// </summary>
    [Fact]
    public async Task Handle_ShouldRemoveTheOrder_WhenTheHeroIsGone()
    {
        var (hero, order) = GivenDueOrder();
        _heroes.GetByIdAsync(hero.Id, Arg.Any<CancellationToken>()).Returns((Hero?)null);

        await Handler().Handle(new CompleteHeroLevelUpCommand(order.Id), CancellationToken.None);

        _heroes.Received(1).RemoveOrder(order);
        await _unitOfWork.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }
}
