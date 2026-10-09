using EmpireIdle.Application.Heroes.Commands;
using EmpireIdle.Application.Interfaces;
using EmpireIdle.Domain.Entities;
using EmpireIdle.Domain.Enums;
using EmpireIdle.Domain.Exceptions;
using EmpireIdle.Domain.Services;
using EmpireIdle.Domain.Services.Config;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Time.Testing;
using NSubstitute;

namespace EmpireIdle.Application.Tests.Heroes;

/// <summary>
/// Посилення артефакта (GDD §6.4, §9.12): згодоване спорядження зникає й передає базу за рідкість
/// плюс увесь вкладений досвід; гаєчки дають свій. Унікальне й вдягнене не годуються.
/// </summary>
public class FeedArtifactCommandTests
{
    private const string Wrench = "wrench_common";
    private static readonly DateTime Now = new(2026, 10, 9, 12, 0, 0, DateTimeKind.Utc);
    private static readonly Guid PlayerId = Guid.NewGuid();

    private readonly IInventoryRepository _inventory = Substitute.For<IInventoryRepository>();
    private readonly IUnitOfWork _unitOfWork = Substitute.For<IUnitOfWork>();
    private readonly GameConfig _config;

    public FeedArtifactCommandTests()
    {
        _config = HeroTestConfig.Create();
        _config.Items.Add(new ItemConfig { Key = Wrench, Type = "wrench", ArtifactExperience = 100 });
        _config.Items.Add(new ItemConfig { Key = "speedup_1m", Type = "speedup", SpeedUpMinutes = 1 });
    }

    private EquipmentItem GivenItem(Rarity rarity = Rarity.Common, Guid? owner = null)
    {
        var item = new EquipmentItem(Guid.NewGuid(), owner ?? PlayerId, 1, HeroTestConfig.Artifact,
            EquipmentSlot.Artifact, rarity, [("Attack", 10.0)], Now);
        _inventory.GetEquipmentByIdAsync(item.Id, Arg.Any<CancellationToken>()).Returns(item);
        return item;
    }

    private PlayerItem GivenWrenches(int count)
    {
        var stack = new PlayerItem(Guid.NewGuid(), PlayerId, Wrench, count);
        _inventory.GetItemAsync(PlayerId, Wrench, Arg.Any<CancellationToken>()).Returns(stack);
        return stack;
    }

    private Task Feed(EquipmentItem target, IReadOnlyList<Guid> food, Dictionary<string, int>? wrenches = null)
        => new FeedArtifactCommandHandler(_inventory, _unitOfWork, new FakeTimeProvider(Now),
                new ArtifactProgression(_config.Equipment), new ArtifactRoller(_config.Equipment),
                new DeterministicRandom(7), new GameCatalog(_config), NullLogger<FeedArtifactCommandHandler>.Instance)
            .Handle(new FeedArtifactCommand(PlayerId, target.Id, food, wrenches ?? []), CancellationToken.None);

    /// <summary>Два звичайні артефакти — 200 досвіду: рівень 2 (до нього 150), обидва зникають.</summary>
    [Fact]
    public async Task Handle_ShouldAddTheFoodsExperience_AndRemoveTheFood()
    {
        var target = GivenItem();
        var first = GivenItem();
        var second = GivenItem();

        await Feed(target, [first.Id, second.Id]);

        Assert.Equal((2, 200L), (target.Level, target.Experience));
        _inventory.Received(1).RemoveEquipment(first);
        _inventory.Received(1).RemoveEquipment(second);
        await _unitOfWork.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    /// <summary>Прокачаний артефакт передає весь вкладений досвід, а не лише базу.</summary>
    [Fact]
    public async Task Handle_ShouldPassAllInvestedExperience_OfALevelledFood()
    {
        var target = GivenItem();
        var levelled = GivenItem(Rarity.Rare);
        levelled.GainExperience(340, 3, Now);

        await Feed(target, [levelled.Id]);

        // 300 за рідкісний + 340 вкладених = 640: рівень 4 (620)
        Assert.Equal((4, 640L), (target.Level, target.Experience));
    }

    /// <summary>Рівень 4 — новий стат: ролл розігрується на кожен пройдений рівень.</summary>
    [Fact]
    public async Task Handle_ShouldRollEveryCrossedLevel()
    {
        var target = GivenItem();
        GivenWrenches(7);

        await Feed(target, [], new Dictionary<string, int> { [Wrench] = 7 });

        Assert.Equal(4, target.Level);
        Assert.Equal(2, target.Stats.Count);
        Assert.Equal([1, 2, 3, 4], target.Rolls.Select(r => r.Level).Order());
    }

    [Fact]
    public async Task Handle_ShouldConsumeWrenches_AndDropAnEmptyStack()
    {
        var target = GivenItem();
        var stack = GivenWrenches(2);

        await Feed(target, [], new Dictionary<string, int> { [Wrench] = 2 });

        Assert.Equal(200L, target.Experience);
        Assert.Equal(0, stack.Count);
        _inventory.Received(1).RemoveItem(stack);
    }

    /// <summary>Досвід понад стелю згорає: предмет на 20 рівні не носить невидимого запасу.</summary>
    [Fact]
    public async Task Handle_ShouldBurnExperienceAboveTheCeiling()
    {
        var progression = new ArtifactProgression(_config.Equipment);
        var target = GivenItem();
        var ceiling = progression.ExperienceToReach(_config.Equipment.MaxLevel);
        target.GainExperience(ceiling - 50, progression.LevelFor(ceiling - 50), Now);
        GivenWrenches(1);

        await Feed(target, [], new Dictionary<string, int> { [Wrench] = 1 });

        Assert.Equal((_config.Equipment.MaxLevel, ceiling), (target.Level, target.Experience));
    }

    [Fact]
    public async Task Handle_ShouldRefuse_AtTheMaxLevel()
    {
        var progression = new ArtifactProgression(_config.Equipment);
        var target = GivenItem();
        target.GainExperience(progression.ExperienceToReach(20), 20, Now);
        var food = GivenItem();

        var refusal = await Assert.ThrowsAsync<RequirementNotMetException>(() => Feed(target, [food.Id]));

        Assert.Equal(RefusalReasons.EquipmentMaxLevel.Key, refusal.Reason);
        _inventory.DidNotReceive().RemoveEquipment(Arg.Any<EquipmentItem>());
    }

    [Fact]
    public async Task Handle_ShouldRefuseUniqueFood_AndKeepEverything()
    {
        var target = GivenItem();
        var plain = GivenItem();
        var unique = GivenItem(Rarity.Unique);

        var refusal = await Assert.ThrowsAsync<RequirementNotMetException>(() => Feed(target, [plain.Id, unique.Id]));

        Assert.Equal(RefusalReasons.EquipmentUniqueFood.Key, refusal.Reason);
        Assert.Equal(0L, target.Experience);
        _inventory.DidNotReceive().RemoveEquipment(Arg.Any<EquipmentItem>());
        await _unitOfWork.DidNotReceive().SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_ShouldRefuseEquippedFood()
    {
        var target = GivenItem();
        var worn = GivenItem();
        worn.EquipTo(Guid.NewGuid(), HeroTestConfig.NecklaceSlot, Now);

        var refusal = await Assert.ThrowsAsync<InvalidStateException>(() => Feed(target, [worn.Id]));

        Assert.Equal(RefusalReasons.EquipmentFoodEquipped.Key, refusal.Reason);
    }

    [Fact]
    public async Task Handle_ShouldRefuseFeedingAnArtifactToItself()
    {
        var target = GivenItem();

        await Assert.ThrowsAsync<RequirementNotMetException>(() => Feed(target, [target.Id]));
    }

    /// <summary>Чужий предмет не відрізняється від неіснуючого — ні як ціль, ні як їжа.</summary>
    [Fact]
    public async Task Handle_ShouldNotFeedSomeoneElsesGear()
    {
        var target = GivenItem();
        var foreign = GivenItem(owner: Guid.NewGuid());

        await Assert.ThrowsAsync<EntityNotFoundException>(() => Feed(target, [foreign.Id]));
        await Assert.ThrowsAsync<EntityNotFoundException>(() => Feed(foreign, [target.Id]));
    }

    [Fact]
    public async Task Handle_ShouldRefuseAnItemThatIsNotAWrench()
    {
        var target = GivenItem();

        await Assert.ThrowsAsync<RequirementNotMetException>(() =>
            Feed(target, [], new Dictionary<string, int> { ["speedup_1m"] = 1 }));
    }

    [Fact]
    public async Task Handle_ShouldRefuse_WhenWrenchesRunShort()
    {
        var target = GivenItem();
        var stack = GivenWrenches(1);

        await Assert.ThrowsAsync<RequirementNotMetException>(() =>
            Feed(target, [], new Dictionary<string, int> { [Wrench] = 2 }));

        Assert.Equal(1, stack.Count);
    }
}
