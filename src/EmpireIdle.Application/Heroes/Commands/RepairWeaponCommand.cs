using EmpireIdle.Application.Common.Security;
using EmpireIdle.Application.Interfaces;
using EmpireIdle.Domain.Exceptions;
using EmpireIdle.Domain.Services;
using EmpireIdle.Domain.Services.Config;
using EmpireIdle.Domain.ValueObjects;
using MediatR;
using Microsoft.Extensions.Logging;

namespace EmpireIdle.Application.Heroes.Commands
{
    /// <summary>Полагодити зламану зброю в кузні.</summary>
    public record RepairWeaponCommand(Guid PlayerId, Guid EquipmentId)
        : IRequest, IPlayerScopedRequest, IIdempotentRequest;

    /// <summary>
    /// Обробник RepairWeaponCommand.
    ///
    /// Ремонт — за gems, не за золото: поломка забирає спробу, а не прогрес,
    /// і саме тут гравець вирішує, чи варта зброя платежу. Ціна росте з рівнем
    /// заточки, бо росте й те, що рятується.
    /// </summary>
    public sealed class RepairWeaponCommandHandler : IRequestHandler<RepairWeaponCommand>
    {
        private readonly IInventoryRepository _inventoryRepository;
        private readonly IVillageRepository _villageRepository;
        private readonly IPlayerRepository _playerRepository;
        private readonly IPlayerWalletRepository _walletRepository;
        private readonly IUnitOfWork _unitOfWork;
        private readonly TimeProvider _timeProvider;
        private readonly EnhancementRules _rules;
        private readonly GameCatalog _catalog;
        private readonly ILogger<RepairWeaponCommandHandler> _logger;

        public RepairWeaponCommandHandler(
            IInventoryRepository inventoryRepository,
            IVillageRepository villageRepository,
            IPlayerRepository playerRepository,
            IPlayerWalletRepository walletRepository,
            IUnitOfWork unitOfWork,
            TimeProvider timeProvider,
            EnhancementRules rules,
            GameCatalog catalog,
            ILogger<RepairWeaponCommandHandler> logger)
        {
            _inventoryRepository = inventoryRepository;
            _villageRepository = villageRepository;
            _playerRepository = playerRepository;
            _walletRepository = walletRepository;
            _unitOfWork = unitOfWork;
            _timeProvider = timeProvider;
            _rules = rules;
            _catalog = catalog;
            _logger = logger;
        }

        public async Task Handle(RepairWeaponCommand request, CancellationToken cancellationToken)
        {
            var now = _timeProvider.GetUtcNow().UtcDateTime;

            var item = await _inventoryRepository.GetEquipmentByIdAsync(request.EquipmentId, cancellationToken)
                ?? throw new EntityNotFoundException("Equipment", request.EquipmentId.ToString());

            if (item.PlayerId != request.PlayerId)
                throw new EntityNotFoundException("Equipment", request.EquipmentId.ToString());

            // Ціле не лагодять — команда ідемпотентна
            if (!item.IsBroken)
                return;

            var village = await _villageRepository.GetByPlayerIdAsync(request.PlayerId, cancellationToken)
                ?? throw new InvalidOperationException($"Village not found for player {request.PlayerId}.");

            var forge = _catalog.Config.Equipment.ForgeBuildingKey;

            if (!village.HasBuilding(forge))
                throw new RequirementNotMetException($"Repairing requires the {forge}.");

            var cost = _rules.RepairGems(item.EnhancementLevel);

            // Гаманець належить акаунту, тож ідемо через Player за UserId
            var player = await _playerRepository.GetByIdAsync(request.PlayerId, cancellationToken)
                ?? throw new EntityNotFoundException("Player", request.PlayerId);

            var wallet = await _walletRepository.GetByUserIdAsync(player.UserId, cancellationToken)
                ?? throw new EntityNotFoundException("Wallet", player.UserId);

            // Перевірка до списання: гравцю потрібна відмова з цифрами
            if (wallet.GemBalance.Value < cost)
                throw new NotEnoughResourcesException("gems", cost, wallet.GemBalance.Value);

            wallet.SpendGems(new GemAmount(cost), $"repair:{item.ItemKey}", request.PlayerId, now);
            item.Repair(now);

            await _unitOfWork.SaveChangesAsync(cancellationToken);

            _logger.LogInformation("Weapon {EquipmentId} repaired at +{Level}", item.Id, item.EnhancementLevel);
        }
    }
}
