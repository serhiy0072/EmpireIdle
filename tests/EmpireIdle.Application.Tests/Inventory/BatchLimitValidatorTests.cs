using EmpireIdle.Application.Inventory.Commands;
using EmpireIdle.Application.Inventory.Validators;
using EmpireIdle.Application.Lootboxes.Commands;
using EmpireIdle.Application.Lootboxes.Validators;

namespace EmpireIdle.Application.Tests.Inventory;

/// <summary>Стек вживається й скрині відкриваються пачкою одним запитом — ліміт лише проти сміття.</summary>
public class BatchLimitValidatorTests
{
    private static readonly Guid PlayerId = Guid.NewGuid();

    [Theory]
    [InlineData(UseItemCommand.MaxCount, true)]
    [InlineData(UseItemCommand.MaxCount + 1, false)]
    public void UseItem_ShouldTakeAWholeStack(int count, bool valid)
        => Assert.Equal(valid, new UseItemCommandValidator().Validate(new UseItemCommand(PlayerId, "hero_xp_1k", count)).IsValid);

    [Theory]
    [InlineData(100, true)]
    [InlineData(101, false)]
    public void OpenLootbox_ShouldOpenAHundredAtOnce(int count, bool valid)
        => Assert.Equal(valid, new OpenLootboxCommandValidator().Validate(new OpenLootboxCommand(PlayerId, "chest_supply", count)).IsValid);
}
