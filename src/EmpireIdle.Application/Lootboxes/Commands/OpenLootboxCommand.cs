using EmpireIdle.Application.Common.Security;
using EmpireIdle.Application.Interfaces;
using EmpireIdle.Application.Lootboxes.Contracts;
using EmpireIdle.Application.Lootboxes.Services;
using EmpireIdle.Domain.Exceptions;
using EmpireIdle.Domain.Services;
using MediatR;
using Microsoft.Extensions.Logging;

namespace EmpireIdle.Application.Lootboxes.Commands
{
    /// <summary>
    /// Відкрити скрині з рюкзака (GDD §9.12) — від однієї до MaxCount за запит. Списання скринь,
    /// лічильник гарантії й видача — одна транзакція.
    /// </summary>
    public record OpenLootboxCommand(Guid PlayerId, string ItemKey, int Count = 1)
        : IRequest<LootboxOpening>, IPlayerScopedRequest, IIdempotentRequest
    {
        public const int MaxCount = 100;
    }

    internal sealed class OpenLootboxCommandHandler : IRequestHandler<OpenLootboxCommand, LootboxOpening>
    {
        private readonly IInventoryRepository _inventory;
        private readonly LootboxOpener _opener;
        private readonly GameCatalog _catalog;
        private readonly IUnitOfWork _unitOfWork;
        private readonly TimeProvider _timeProvider;
        private readonly ILogger<OpenLootboxCommandHandler> _logger;

        public OpenLootboxCommandHandler(
            IInventoryRepository inventory,
            LootboxOpener opener,
            GameCatalog catalog,
            IUnitOfWork unitOfWork,
            TimeProvider timeProvider,
            ILogger<OpenLootboxCommandHandler> logger)
        {
            _inventory = inventory;
            _opener = opener;
            _catalog = catalog;
            _unitOfWork = unitOfWork;
            _timeProvider = timeProvider;
            _logger = logger;
        }

        public async Task<LootboxOpening> Handle(OpenLootboxCommand request, CancellationToken cancellationToken)
        {
            var box = _catalog.FindLootbox(request.ItemKey)
                ?? throw new EntityNotFoundException("Lootbox", request.ItemKey);

            var stack = await _inventory.GetItemAsync(request.PlayerId, box.Key, cancellationToken);

            if (stack is null || stack.Count < request.Count)
                throw new RequirementNotMetException(RefusalReasons.ItemNotEnough,
                    $"Not enough '{box.Key}': {stack?.Count ?? 0} of {request.Count}.",
                    _catalog.Item(box.Key).DisplayName, request.Count, stack?.Count ?? 0);

            stack.Consume(request.Count);

            if (stack.Count == 0)
                _inventory.RemoveItem(stack);

            var now = _timeProvider.GetUtcNow().UtcDateTime;
            var drops = await _opener.OpenAsync(request.PlayerId, box, request.Count, $"lootbox:{box.Key}", now, cancellationToken);

            await _unitOfWork.SaveChangesAsync(cancellationToken);

            _logger.LogInformation("Player {PlayerId} opened {Count} × {Lootbox}: {Drops}",
                request.PlayerId, request.Count, box.Key, string.Join(", ", drops.Select(d => $"{d.Type}:{d.Key}×{d.Amount}")));

            return new LootboxOpening(box.Key, drops);
        }
    }
}
