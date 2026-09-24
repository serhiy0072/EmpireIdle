using EmpireIdle.Application.Common.Services;
using EmpireIdle.Application.Interfaces;
using EmpireIdle.Application.Inventory.Commands;
using EmpireIdle.Domain.Entities;
using EmpireIdle.Domain.Exceptions;
using EmpireIdle.Domain.Services;
using EmpireIdle.Domain.Services.Config;
using EmpireIdle.TestKit;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;

namespace EmpireIdle.Application.Tests.Inventory;

/// <summary>
/// Дарування (GDD §8.8): лише члену свого клану, лише те, що дарується,
/// і лише з наявного — інакше предмет з'явився б нізвідки.
/// </summary>
public class GiftItemCommandTests
{
    private static readonly DateTime Now = TestKit.Entities.Now;
    private static readonly Guid ClanId = Guid.NewGuid();

    private readonly IInventoryRepository _inventory = Substitute.For<IInventoryRepository>();
    private readonly IPlayerRepository _players = Substitute.For<IPlayerRepository>();
    private readonly IUnitOfWork _unitOfWork = Substitute.For<IUnitOfWork>();

    private readonly Player _giver = NewPlayer(ClanId);
    private readonly Player _clanmate = NewPlayer(ClanId);

    public GiftItemCommandTests()
    {
        _players.GetByIdAsync(_giver.Id, Arg.Any<CancellationToken>()).Returns(_giver);
        _players.GetByIdAsync(_clanmate.Id, Arg.Any<CancellationToken>()).Returns(_clanmate);
    }

    private static Player NewPlayer(Guid? clanId)
    {
        var player = new Player(Guid.NewGuid(), "p", "p@example.com", Guid.NewGuid().ToString(), Now);

        if (clanId is { } id)
            player.JoinClan(id);

        return player;
    }

    private GiftItemCommandHandler Handler()
    {
        var config = new GameConfigBuilder().WithBuildings().Build();
        config.Items =
        [
            new ItemConfig { Key = "teleport", DisplayName = "Телепорт", Description = "", Type = "teleport", Giftable = true },
            new ItemConfig { Key = "food_crate", DisplayName = "Ящик їжі", Description = "", Type = "resource" }
        ];
        var catalog = new GameCatalog(config);

        return new GiftItemCommandHandler(_inventory, _players, _unitOfWork,
            new ItemGranter(_inventory, Substitute.For<IServerContext>(), Substitute.For<IRandomSource>(),
                new ArtifactRoller(config.Equipment)),
            catalog, NullLogger<GiftItemCommandHandler>.Instance);
    }

    private PlayerItem GivenStack(string key, int count)
    {
        var stack = new PlayerItem(Guid.NewGuid(), _giver.Id, key, count);
        _inventory.GetItemAsync(_giver.Id, key, Arg.Any<CancellationToken>()).Returns(stack);
        return stack;
    }

    [Fact]
    public async Task Gift_ShouldMoveTheItemToAClanmate()
    {
        var stack = GivenStack("teleport", 3);
        var recipientStack = new PlayerItem(Guid.NewGuid(), _clanmate.Id, "teleport", 1);
        _inventory.GetItemAsync(_clanmate.Id, "teleport", Arg.Any<CancellationToken>()).Returns(recipientStack);

        await Handler().Handle(new GiftItemCommand(_giver.Id, _clanmate.Id, "teleport", 2), CancellationToken.None);

        Assert.Equal(1, stack.Count);
        Assert.Equal(3, recipientStack.Count);
        await _unitOfWork.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    /// <summary>Останній предмет стека — стек прибирається, а не лишається порожнім.</summary>
    [Fact]
    public async Task Gift_ShouldRemoveTheEmptyStack()
    {
        var stack = GivenStack("teleport", 1);

        await Handler().Handle(new GiftItemCommand(_giver.Id, _clanmate.Id, "teleport", 1), CancellationToken.None);

        _inventory.Received(1).RemoveItem(stack);
        await _inventory.Received(1).AddItemAsync(
            Arg.Is<PlayerItem>(i => i.PlayerId == _clanmate.Id && i.ItemKey == "teleport" && i.Count == 1),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Gift_ShouldRefuse_ToAPlayerFromAnotherClan()
    {
        GivenStack("teleport", 1);
        var stranger = NewPlayer(Guid.NewGuid());
        _players.GetByIdAsync(stranger.Id, Arg.Any<CancellationToken>()).Returns(stranger);

        var refusal = await Assert.ThrowsAsync<RequirementNotMetException>(() =>
            Handler().Handle(new GiftItemCommand(_giver.Id, stranger.Id, "teleport", 1), CancellationToken.None));

        Assert.Equal(RefusalReasons.ItemGiftNotClanmate.Key, refusal.Reason);
    }

    /// <summary>Двоє без клану — не соратники: «обидва null» не означає «той самий клан».</summary>
    [Fact]
    public async Task Gift_ShouldRefuse_BetweenPlayersWithoutAClan()
    {
        var loner = NewPlayer(null);
        var otherLoner = NewPlayer(null);
        _players.GetByIdAsync(loner.Id, Arg.Any<CancellationToken>()).Returns(loner);
        _players.GetByIdAsync(otherLoner.Id, Arg.Any<CancellationToken>()).Returns(otherLoner);

        var refusal = await Assert.ThrowsAsync<RequirementNotMetException>(() =>
            Handler().Handle(new GiftItemCommand(loner.Id, otherLoner.Id, "teleport", 1), CancellationToken.None));

        Assert.Equal(RefusalReasons.ItemGiftNotClanmate.Key, refusal.Reason);
    }

    [Fact]
    public async Task Gift_ShouldRefuse_AnItemThatIsNotGiftable()
    {
        GivenStack("food_crate", 5);

        var refusal = await Assert.ThrowsAsync<RequirementNotMetException>(() =>
            Handler().Handle(new GiftItemCommand(_giver.Id, _clanmate.Id, "food_crate", 1), CancellationToken.None));

        Assert.Equal(RefusalReasons.ItemNotGiftable.Key, refusal.Reason);
    }

    [Fact]
    public async Task Gift_ShouldRefuse_ToOneself()
    {
        GivenStack("teleport", 1);

        var refusal = await Assert.ThrowsAsync<RequirementNotMetException>(() =>
            Handler().Handle(new GiftItemCommand(_giver.Id, _giver.Id, "teleport", 1), CancellationToken.None));

        Assert.Equal(RefusalReasons.ItemGiftToSelf.Key, refusal.Reason);
    }

    [Fact]
    public async Task Gift_ShouldRefuse_MoreThanTheGiverHas()
    {
        var stack = GivenStack("teleport", 1);

        var refusal = await Assert.ThrowsAsync<RequirementNotMetException>(() =>
            Handler().Handle(new GiftItemCommand(_giver.Id, _clanmate.Id, "teleport", 2), CancellationToken.None));

        Assert.Equal(RefusalReasons.ItemNotEnough.Key, refusal.Reason);
        Assert.Equal(1, stack.Count);
    }
}
