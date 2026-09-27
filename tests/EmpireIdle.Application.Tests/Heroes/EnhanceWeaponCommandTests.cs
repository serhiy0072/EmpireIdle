using EmpireIdle.Application.Heroes.Commands;
using EmpireIdle.Application.Interfaces;
using EmpireIdle.Domain.Entities;
using EmpireIdle.Domain.Enums;
using EmpireIdle.Domain.Exceptions;
using EmpireIdle.Domain.Services;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Time.Testing;
using NSubstitute;

namespace EmpireIdle.Application.Tests.Heroes;

/// <summary>
/// Зброя, виставлена на ринок, у заставі: заточувати її не можна взагалі. Інакше провал
/// комітив би золото за спробу, а поломка продавала б покупцю зламаний лот.
/// </summary>
public class EnhanceWeaponCommandTests
{
    private static readonly DateTime Now = new(2026, 9, 27, 12, 0, 0, DateTimeKind.Utc);
    private static readonly Guid PlayerId = Guid.NewGuid();
    private const int ServerId = 1;

    private readonly IInventoryRepository _inventory = Substitute.For<IInventoryRepository>();
    private readonly IVillageRepository _villages = Substitute.For<IVillageRepository>();
    private readonly IUnitOfWork _unitOfWork = Substitute.For<IUnitOfWork>();

    private EnhanceWeaponCommandHandler Handler()
    {
        var config = HeroTestConfig.Create();

        return new EnhanceWeaponCommandHandler(
            _inventory, _villages, _unitOfWork, new FakeTimeProvider(Now), new EnhancementRules(config.Equipment),
            Substitute.For<IRandomSource>(), new GameCatalog(config), NullLogger<EnhanceWeaponCommandHandler>.Instance);
    }

    [Fact]
    public async Task Handle_ShouldRefuse_BeforeChargingGold_WhenTheWeaponIsOnTheMarket()
    {
        var sword = new EquipmentItem(Guid.NewGuid(), PlayerId, ServerId, "sword_iron", EquipmentSlot.Weapon,
            Rarity.Common, [("Attack", 10.0)], Now);
        sword.PutOnMarket(Now);
        _inventory.GetEquipmentByIdAsync(sword.Id, Arg.Any<CancellationToken>()).Returns(sword);

        var refusal = await Assert.ThrowsAsync<InvalidStateException>(() =>
            Handler().Handle(new EnhanceWeaponCommand(PlayerId, sword.Id), CancellationToken.None));

        Assert.Equal(RefusalReasons.MarketItemListed.Key, refusal.Reason);
        Assert.False(sword.IsBroken);

        // До села — а отже й до списання золота — справа не дійшла
        await _villages.DidNotReceiveWithAnyArgs().GetByPlayerIdAsync(default, default);
        await _unitOfWork.DidNotReceiveWithAnyArgs().SaveChangesAsync(default);
    }

    /// <summary>Сам домен теж не ламає лот: покупець не має отримати зламану зброю.</summary>
    [Fact]
    public void Break_ShouldRefuse_AListedWeapon()
    {
        var sword = new EquipmentItem(Guid.NewGuid(), PlayerId, ServerId, "sword_iron", EquipmentSlot.Weapon,
            Rarity.Common, [("Attack", 10.0)], Now);
        sword.PutOnMarket(Now);

        Assert.Throws<InvalidStateException>(() => sword.Break(Now));
        Assert.False(sword.IsBroken);
    }
}
