using EmpireIdle.Application.Interfaces;
using EmpireIdle.Application.Inventory.Commands;
using EmpireIdle.Application.Inventory.Contracts;
using EmpireIdle.Application.Inventory.Effects;
using EmpireIdle.Domain.Entities;
using EmpireIdle.Domain.Exceptions;
using EmpireIdle.Domain.Services;
using EmpireIdle.Domain.Services.Config;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Time.Testing;
using NSubstitute;

namespace EmpireIdle.Application.Tests.Inventory;

/// <summary>
/// Використання предмета. Ключове — порядок: ефект застосовується ДО списання,
/// тому невдале застосування не з'їдає предмет.
/// </summary>
public class UseItemCommandTests
{
    private static readonly DateTime Now = new(2026, 6, 1, 12, 0, 0, DateTimeKind.Utc);
    private static readonly Guid PlayerId = Guid.NewGuid();

    private readonly IInventoryRepository _inventory = Substitute.For<IInventoryRepository>();
    private readonly IItemEffect _effect = Substitute.For<IItemEffect>();
    private readonly IUnitOfWork _unitOfWork = Substitute.For<IUnitOfWork>();

    private static GameConfig Config() => new()
    {
        Buildings = [new BuildingConfig { Key = "townhall", IsMainBuilding = true }],
        Items =
        [
            new ItemConfig
            {
                Key = "resource_pack",
                DisplayName = "Resource Pack",
                Type = "resource",
                IsStackable = true
            }
        ]
    };

    public UseItemCommandTests() => _effect.ItemType.Returns("resource");

    private UseItemCommandHandler Handler() => new(
        _inventory, new ItemEffectDispatcher([_effect]), _unitOfWork,
        new GameCatalog(Config()), new FakeTimeProvider(Now),
        NullLogger<UseItemCommandHandler>.Instance);

    private PlayerItem GivenStack(int count = 5)
    {
        var stack = new PlayerItem(Guid.NewGuid(), PlayerId, "resource_pack", count);

        _inventory.GetItemAsync(PlayerId, "resource_pack", Arg.Any<CancellationToken>()).Returns(stack);

        return stack;
    }

    private static UseItemCommand Use(int count = 1) => new(PlayerId, "resource_pack", count, null);

    /// <summary>Ефект застосовується, предмет списується.</summary>
    [Fact]
    public async Task Handle_ShouldApplyTheEffectAndConsumeTheItem()
    {
        var stack = GivenStack(count: 5);

        await Handler().Handle(Use(2), CancellationToken.None);

        await _effect.Received(1).ApplyAsync(
            Arg.Is<ItemUsageContext>(c => c.PlayerId == PlayerId && c.Count == 2),
            Arg.Any<CancellationToken>());

        Assert.Equal(3, stack.Count);
    }

    /// <summary>
    /// Невдалий ефект не списує предмет: застосування йде до списання,
    /// і виняток зупиняє операцію до SaveChanges.
    /// </summary>
    [Fact]
    public async Task Handle_ShouldNotConsume_WhenTheEffectFails()
    {
        var stack = GivenStack(count: 5);

        _effect.ApplyAsync(Arg.Any<ItemUsageContext>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromException(new RequirementNotMetException("nope")));

        await Assert.ThrowsAsync<RequirementNotMetException>(() =>
            Handler().Handle(Use(2), CancellationToken.None));

        Assert.Equal(5, stack.Count);
        await _unitOfWork.DidNotReceive().SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    /// <summary>Останній предмет прибирає стек із інвентаря.</summary>
    [Fact]
    public async Task Handle_ShouldRemoveTheStack_WhenItRunsOut()
    {
        var stack = GivenStack(count: 1);

        await Handler().Handle(Use(1), CancellationToken.None);

        _inventory.Received(1).RemoveItem(stack);
    }

    /// <summary>Предмета немає в інвентарі — 404.</summary>
    [Fact]
    public async Task Handle_ShouldThrow_WhenTheItemIsNotOwned()
    {
        _inventory.GetItemAsync(PlayerId, "resource_pack", Arg.Any<CancellationToken>())
            .Returns((PlayerItem?)null);

        await Assert.ThrowsAsync<EntityNotFoundException>(() =>
            Handler().Handle(Use(), CancellationToken.None));
    }

    /// <summary>Невідомий ключ предмета — 404, бо він прийшов від гравця.</summary>
    [Fact]
    public async Task Handle_ShouldThrow_ForUnknownItemKey()
    {
        await Assert.ThrowsAsync<EntityNotFoundException>(() =>
            Handler().Handle(new UseItemCommand(PlayerId, "dragon_egg", 1, null), CancellationToken.None));
    }

    /// <summary>Нульова або від'ємна кількість — помилка запиту.</summary>
    [Theory]
    [InlineData(0)]
    [InlineData(-3)]
    public async Task Handle_ShouldReject_NonPositiveCount(int count)
    {
        GivenStack();

        await Assert.ThrowsAsync<RequirementNotMetException>(() =>
            Handler().Handle(Use(count), CancellationToken.None));
    }

    /// <summary>Координати доходять до ефекту — без них телепорт не спрацює.</summary>
    [Fact]
    public async Task Handle_ShouldPassTargetCoordinatesToTheEffect()
    {
        GivenStack();

        await Handler().Handle(
            new UseItemCommand(PlayerId, "resource_pack", 1, null, TargetX: 42, TargetY: 17),
            CancellationToken.None);

        await _effect.Received(1).ApplyAsync(
            Arg.Is<ItemUsageContext>(c => c.TargetX == 42 && c.TargetY == 17),
            Arg.Any<CancellationToken>());
    }
}
