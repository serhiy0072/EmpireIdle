using EmpireIdle.Application.Common.Security;
using EmpireIdle.Application.Common.Services;
using EmpireIdle.Application.Interfaces;
using EmpireIdle.Domain.Enums;
using EmpireIdle.Domain.Exceptions;
using EmpireIdle.Domain.Services;
using EmpireIdle.Domain.Services.Config;
using MediatR;
using Microsoft.Extensions.Logging;

namespace EmpireIdle.Application.Heroes.Commands
{
    /// <summary>Купити зброю в кузні за золото.</summary>
    public record BuyWeaponCommand(Guid PlayerId, string ItemKey)
        : IRequest, IPlayerScopedRequest, IIdempotentRequest;

    /// <summary>
    /// Обробник BuyWeaponCommand.
    ///
    /// Зброя — єдиний предмет спорядження, який купують: артефакти падають
    /// у данжах, і ціни в них немає навмисно. Тому клас героя тут не
    /// перевіряється: гравець вільний скласти арсенал наперед, а придатність
    /// вирішується на вдяганні.
    ///
    /// Куплена зброя приходить із базовими статами свого типу, без заточки:
    /// заточка — окремий ризик і окремі гроші.
    /// </summary>
    public sealed class BuyWeaponCommandHandler : IRequestHandler<BuyWeaponCommand>
    {
        private readonly IVillageRepository _villageRepository;
        private readonly IUnitOfWork _unitOfWork;
        private readonly ItemGranter _granter;
        private readonly TimeProvider _timeProvider;
        private readonly GameCatalog _catalog;
        private readonly ILogger<BuyWeaponCommandHandler> _logger;

        public BuyWeaponCommandHandler(
            IVillageRepository villageRepository,
            IUnitOfWork unitOfWork,
            ItemGranter granter,
            TimeProvider timeProvider,
            GameCatalog catalog,
            ILogger<BuyWeaponCommandHandler> logger)
        {
            _villageRepository = villageRepository;
            _unitOfWork = unitOfWork;
            _granter = granter;
            _timeProvider = timeProvider;
            _catalog = catalog;
            _logger = logger;
        }

        public async Task Handle(BuyWeaponCommand request, CancellationToken cancellationToken)
        {
            var now = _timeProvider.GetUtcNow().UtcDateTime;

            var config = _catalog.Items.GetValueOrDefault(request.ItemKey)
                ?? throw new EntityNotFoundException("Item", request.ItemKey);

            if (config.Slot != EquipmentSlot.Weapon)
                throw new RequirementNotMetException($"Item '{request.ItemKey}' is not a weapon.");

            // Ціна перевірена валідатором, але нуль тут означав би безкоштовну
            // зброю — надто дорога помилка, щоб покладатись на старт застосунку
            if (config.PriceGold < 1)
                throw new RequirementNotMetException($"Weapon '{request.ItemKey}' has no price.");

            var village = await _villageRepository.GetByPlayerIdAsync(request.PlayerId, cancellationToken)
                ?? throw new InvalidOperationException($"Village not found for player {request.PlayerId}.");

            var forge = _catalog.Config.Equipment.ForgeBuildingKey;

            if (!village.HasBuilding(forge))
                throw new RequirementNotMetException(RefusalReasons.BuildingRequired,
                    $"Buying a weapon requires the {forge}.", _catalog.Building(forge).DisplayName);

            village.ChargeCost([new ResourceCost { Resource = "gold", Amount = config.PriceGold }], now);

            await _granter.GrantEquipmentAsync(
                request.PlayerId, config.Key, EquipmentSlot.Weapon, config.Rarity,
                config.BaseStats.Select(s => (s.Key, s.Value)), now, cancellationToken);

            await _unitOfWork.SaveChangesAsync(cancellationToken);

            _logger.LogInformation("Player {PlayerId} bought weapon {ItemKey} for {Gold} gold",
                request.PlayerId, config.Key, config.PriceGold);
        }
    }
}
