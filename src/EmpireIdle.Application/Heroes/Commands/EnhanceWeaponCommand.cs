using EmpireIdle.Application.Common.Security;
using EmpireIdle.Application.Interfaces;
using EmpireIdle.Domain.Enums;
using EmpireIdle.Domain.Exceptions;
using EmpireIdle.Domain.Services;
using EmpireIdle.Domain.Services.Config;
using MediatR;
using Microsoft.Extensions.Logging;

namespace EmpireIdle.Application.Heroes.Commands
{
    /// <summary>Заточити зброю на один рівень.</summary>
    public record EnhanceWeaponCommand(Guid PlayerId, Guid EquipmentId) : IRequest<EnhancementOutcome>,
        IPlayerScopedRequest, IIdempotentRequest;

    /// <summary>
    /// Обробник EnhanceWeaponCommand.
    ///
    /// Золото списується до кидка й не повертається: гравець платить за
    /// спробу, а не за результат. Інакше провал нічого не коштував би,
    /// і заточка перестала б бути ризиком.
    ///
    /// Ідемпотентність тут особливо важлива: повтор запиту не має дати
    /// другий кидок. Її забезпечує IIdempotentRequest — резерв ключа
    /// стоїть до виконання.
    /// </summary>
    public sealed class EnhanceWeaponCommandHandler : IRequestHandler<EnhanceWeaponCommand, EnhancementOutcome>
    {
        private readonly IInventoryRepository _inventoryRepository;
        private readonly IVillageRepository _villageRepository;
        private readonly IUnitOfWork _unitOfWork;
        private readonly TimeProvider _timeProvider;
        private readonly EnhancementRules _rules;
        private readonly IRandomSource _random;
        private readonly GameCatalog _catalog;
        private readonly ILogger<EnhanceWeaponCommandHandler> _logger;

        public EnhanceWeaponCommandHandler(
            IInventoryRepository inventoryRepository,
            IVillageRepository villageRepository,
            IUnitOfWork unitOfWork,
            TimeProvider timeProvider,
            EnhancementRules rules,
            IRandomSource random,
            GameCatalog catalog,
            ILogger<EnhanceWeaponCommandHandler> logger)
        {
            _inventoryRepository = inventoryRepository;
            _villageRepository = villageRepository;
            _unitOfWork = unitOfWork;
            _timeProvider = timeProvider;
            _rules = rules;
            _random = random;
            _catalog = catalog;
            _logger = logger;
        }

        public async Task<EnhancementOutcome> Handle(EnhanceWeaponCommand request, CancellationToken cancellationToken)
        {
            var now = _timeProvider.GetUtcNow().UtcDateTime;

            var item = await _inventoryRepository.GetEquipmentByIdAsync(request.EquipmentId, cancellationToken)
                ?? throw new EntityNotFoundException("Equipment", request.EquipmentId.ToString());

            if (item.PlayerId != request.PlayerId)
                throw new EntityNotFoundException("Equipment", request.EquipmentId.ToString());

            if (item.Slot != EquipmentSlot.Weapon)
                throw new RequirementNotMetException($"Equipment {item.Id} is not a weapon.");

            if (item.IsBroken)
                throw new RequirementNotMetException(RefusalReasons.EquipmentBroken, $"Equipment {item.Id} is broken and must be repaired first.");

            var equipment = _catalog.Config.Equipment;

            if (item.EnhancementLevel >= equipment.MaxEnhancement)
                throw new RequirementNotMetException(RefusalReasons.EquipmentMaxEnhancement,
                    $"Equipment {item.Id} is already at the ceiling of +{equipment.MaxEnhancement}.", equipment.MaxEnhancement);

            var village = await _villageRepository.GetByPlayerIdAsync(request.PlayerId, cancellationToken)
                ?? throw new InvalidOperationException($"Village not found for player {request.PlayerId}.");

            if (!village.HasBuilding(equipment.ForgeBuildingKey))
                throw new RequirementNotMetException(RefusalReasons.BuildingRequired,
                    $"Enhancing requires the {equipment.ForgeBuildingKey}.", _catalog.Building(equipment.ForgeBuildingKey).DisplayName);

            // Платимо за спробу, а не за результат
            village.ChargeCost(
                [new ResourceCost { Resource = "gold", Amount = _rules.EnhanceCost(item.EnhancementLevel) }], now);

            var outcome = _rules.Roll(item.EnhancementLevel, _random);

            switch (outcome)
            {
                case EnhancementOutcome.Success:
                    item.Enhance(now);
                    break;

                case EnhancementOutcome.Broken:
                    // Поломка знімає зброю з героя: битися нею більше не можна
                    item.Break(now);
                    break;
            }

            await _unitOfWork.SaveChangesAsync(cancellationToken);

            _logger.LogInformation("Weapon {EquipmentId} enhancement: {Outcome}, level {Level}",
                item.Id, outcome, item.EnhancementLevel);

            return outcome;
        }
    }
}
