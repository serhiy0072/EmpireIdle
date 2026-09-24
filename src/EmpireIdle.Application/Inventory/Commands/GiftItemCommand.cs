using EmpireIdle.Application.Common.Security;
using EmpireIdle.Application.Common.Services;
using EmpireIdle.Application.Interfaces;
using EmpireIdle.Domain.Exceptions;
using EmpireIdle.Domain.Services;
using MediatR;
using Microsoft.Extensions.Logging;

namespace EmpireIdle.Application.Inventory.Commands
{
    /// <summary>
    /// Подарувати стаковий предмет члену свого клану (GDD §8.8). Поки що
    /// дарується лише телепорт: так клан підтягує своїх ближче одне до одного.
    /// </summary>
    public record GiftItemCommand(Guid PlayerId, Guid RecipientId, string ItemKey, int Count)
        : IRequest, IPlayerScopedRequest, IIdempotentRequest;

    public sealed class GiftItemCommandHandler : IRequestHandler<GiftItemCommand>
    {
        private readonly IInventoryRepository _inventoryRepository;
        private readonly IPlayerRepository _playerRepository;
        private readonly IUnitOfWork _unitOfWork;
        private readonly ItemGranter _granter;
        private readonly GameCatalog _catalog;
        private readonly ILogger<GiftItemCommandHandler> _logger;

        public GiftItemCommandHandler(
            IInventoryRepository inventoryRepository,
            IPlayerRepository playerRepository,
            IUnitOfWork unitOfWork,
            ItemGranter granter,
            GameCatalog catalog,
            ILogger<GiftItemCommandHandler> logger)
        {
            _inventoryRepository = inventoryRepository;
            _playerRepository = playerRepository;
            _unitOfWork = unitOfWork;
            _granter = granter;
            _catalog = catalog;
            _logger = logger;
        }

        public async Task Handle(GiftItemCommand request, CancellationToken cancellationToken)
        {
            var config = _catalog.FindItem(request.ItemKey)
                ?? throw new EntityNotFoundException("Item", request.ItemKey);

            if (!config.Giftable)
                throw new RequirementNotMetException(RefusalReasons.ItemNotGiftable, $"Item '{request.ItemKey}' cannot be gifted.");

            if (request.RecipientId == request.PlayerId)
                throw new RequirementNotMetException(RefusalReasons.ItemGiftToSelf, "A gift needs another player.");

            var giver = await _playerRepository.GetByIdAsync(request.PlayerId, cancellationToken)
                ?? throw new EntityNotFoundException("Player", request.PlayerId);

            // Отримувач шукається в тому ж світі (query-фільтр), тож чужий світ — як «не соратник»
            var recipient = await _playerRepository.GetByIdAsync(request.RecipientId, cancellationToken);

            if (giver.ClanId is null || recipient is null || recipient.ClanId != giver.ClanId)
                throw new RequirementNotMetException(RefusalReasons.ItemGiftNotClanmate, "Gifts go to clanmates only.");

            var stack = await _inventoryRepository.GetItemAsync(request.PlayerId, request.ItemKey, cancellationToken);
            var have = stack?.Count ?? 0;

            if (stack is null || have < request.Count)
                throw new RequirementNotMetException(RefusalReasons.ItemNotEnough,
                    $"Not enough '{request.ItemKey}' to gift.", config.DisplayName, request.Count, have);

            stack.Consume(request.Count);

            if (stack.Count == 0)
                _inventoryRepository.RemoveItem(stack);

            await _granter.GrantAsync(request.RecipientId, request.ItemKey, request.Count, cancellationToken);

            await _unitOfWork.SaveChangesAsync(cancellationToken);

            _logger.LogInformation("Player {PlayerId} gifted {Count}x {ItemKey} to {RecipientId}",
                request.PlayerId, request.Count, request.ItemKey, request.RecipientId);
        }
    }
}
