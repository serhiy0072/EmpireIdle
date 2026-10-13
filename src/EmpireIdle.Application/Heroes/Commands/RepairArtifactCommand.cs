using EmpireIdle.Application.Common.Security;
using EmpireIdle.Application.Interfaces;
using EmpireIdle.Domain.Exceptions;
using EmpireIdle.Domain.Services;
using EmpireIdle.Domain.ValueObjects;
using MediatR;
using Microsoft.Extensions.Logging;

namespace EmpireIdle.Application.Heroes.Commands
{
    /// <summary>
    /// Ремонт артефакта, зламаного невдалою заточкою (GDD §9.12): за gems — ціна росте з заточкою,
    /// або одним ремкомплектом на будь-якій заточці.
    /// </summary>
    /// <param name="UseKit">true — платити ремкомплектом, false — gems.</param>
    public record RepairArtifactCommand(Guid PlayerId, Guid EquipmentId, bool UseKit)
        : IRequest, IPlayerScopedRequest, IIdempotentRequest;

    public sealed class RepairArtifactCommandHandler : IRequestHandler<RepairArtifactCommand>
    {
        private readonly IInventoryRepository _inventoryRepository;
        private readonly IPlayerRepository _playerRepository;
        private readonly IPlayerWalletRepository _walletRepository;
        private readonly IUnitOfWork _unitOfWork;
        private readonly TimeProvider _timeProvider;
        private readonly MasteryRules _rules;
        private readonly GameCatalog _catalog;
        private readonly ILogger<RepairArtifactCommandHandler> _logger;

        public RepairArtifactCommandHandler(
            IInventoryRepository inventoryRepository,
            IPlayerRepository playerRepository,
            IPlayerWalletRepository walletRepository,
            IUnitOfWork unitOfWork,
            TimeProvider timeProvider,
            MasteryRules rules,
            GameCatalog catalog,
            ILogger<RepairArtifactCommandHandler> logger)
        {
            _inventoryRepository = inventoryRepository;
            _playerRepository = playerRepository;
            _walletRepository = walletRepository;
            _unitOfWork = unitOfWork;
            _timeProvider = timeProvider;
            _rules = rules;
            _catalog = catalog;
            _logger = logger;
        }

        public async Task Handle(RepairArtifactCommand request, CancellationToken cancellationToken)
        {
            var now = _timeProvider.GetUtcNow().UtcDateTime;

            var item = await _inventoryRepository.GetEquipmentByIdAsync(request.EquipmentId, cancellationToken);

            // Чужий предмет не відрізняється від неіснуючого
            if (item is null || item.PlayerId != request.PlayerId)
                throw new EntityNotFoundException("Equipment", request.EquipmentId.ToString());

            // До оплати: цілий чи виставлений предмет не має коштувати ні gems, ні ремкомплекту
            item.EnsureNotOnMarket();

            if (!item.IsBroken)
                throw new InvalidStateException(RefusalReasons.EquipmentNotBroken, $"Equipment {item.Id} is not broken.");

            if (request.UseKit)
                await PayWithKitAsync(request.PlayerId, cancellationToken);
            else
                await PayWithGemsAsync(request.PlayerId, _rules.RepairGems(item.Mastery), item.Id, now, cancellationToken);

            item.Repair(now);

            await _unitOfWork.SaveChangesAsync(cancellationToken);

            _logger.LogInformation("Artifact {EquipmentId} repaired with {Payment}", item.Id, request.UseKit ? "a kit" : "gems");
        }

        private async Task PayWithKitAsync(Guid playerId, CancellationToken cancellationToken)
        {
            var key = _catalog.Config.Equipment.RepairKitItemKey;
            var stack = await _inventoryRepository.GetItemAsync(playerId, key, cancellationToken);

            if (stack is null || stack.Count < 1)
                throw new RequirementNotMetException(RefusalReasons.ItemNotEnough, $"No '{key}' to repair with.",
                    _catalog.FindItem(key)?.DisplayName ?? key, 1, 0);

            stack.Consume(1);

            if (stack.Count == 0)
                _inventoryRepository.RemoveItem(stack);
        }

        private async Task PayWithGemsAsync(Guid playerId, int price, Guid equipmentId, DateTime now,
            CancellationToken cancellationToken)
        {
            var player = await _playerRepository.GetByIdAsync(playerId, cancellationToken)
                ?? throw new EntityNotFoundException("Player", playerId);

            // Гаманець належить акаунту, тож ідемо через Player за UserId
            var wallet = await _walletRepository.GetByUserIdAsync(player.UserId, cancellationToken)
                ?? throw new EntityNotFoundException("Wallet", player.UserId);

            // Перевірка до списання: гравцю потрібна відмова з цифрами, а не виняток value object
            if (wallet.GemBalance.Value < price)
                throw new NotEnoughResourcesException("gems", price, wallet.GemBalance.Value);

            wallet.SpendGems(new GemAmount(price), $"repair:{equipmentId}", playerId, now);
        }
    }
}
