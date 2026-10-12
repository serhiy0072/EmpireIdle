using EmpireIdle.Application.Common.Security;
using EmpireIdle.Application.Interfaces;
using EmpireIdle.Application.Lootboxes.Contracts;
using EmpireIdle.Application.Lootboxes.Services;
using EmpireIdle.Domain.Entities;
using EmpireIdle.Domain.Exceptions;
using EmpireIdle.Domain.Services;
using MediatR;
using Microsoft.Extensions.Logging;

namespace EmpireIdle.Application.Lootboxes.Commands
{
    /// <summary>
    /// Відкрити ключову скриню (рішення 12.10.2026): безкоштовно — рівно одну, коли відкриття готове,
    /// або ключами — від однієї до MaxCount, по ключу за скриню. Безкоштовне й платне в одному
    /// запиті не змішуються: гравець обирає явно, щоб ключ не згорів, коли безкоштовне ще є.
    /// </summary>
    /// <param name="Free">true — безкоштовне відкриття (Count має бути 1), false — ключами.</param>
    public record OpenKeyChestCommand(Guid PlayerId, string ChestKey, bool Free, int Count = 1)
        : IRequest<LootboxOpening>, IPlayerScopedRequest, IIdempotentRequest
    {
        public const int MaxCount = 100;
    }

    internal sealed class OpenKeyChestCommandHandler : IRequestHandler<OpenKeyChestCommand, LootboxOpening>
    {
        private readonly IInventoryRepository _inventory;
        private readonly ILootboxRepository _lootboxes;
        private readonly LootboxOpener _opener;
        private readonly GameCatalog _catalog;
        private readonly IUnitOfWork _unitOfWork;
        private readonly TimeProvider _timeProvider;
        private readonly ILogger<OpenKeyChestCommandHandler> _logger;

        public OpenKeyChestCommandHandler(
            IInventoryRepository inventory,
            ILootboxRepository lootboxes,
            LootboxOpener opener,
            GameCatalog catalog,
            IUnitOfWork unitOfWork,
            TimeProvider timeProvider,
            ILogger<OpenKeyChestCommandHandler> logger)
        {
            _inventory = inventory;
            _lootboxes = lootboxes;
            _opener = opener;
            _catalog = catalog;
            _unitOfWork = unitOfWork;
            _timeProvider = timeProvider;
            _logger = logger;
        }

        public async Task<LootboxOpening> Handle(OpenKeyChestCommand request, CancellationToken cancellationToken)
        {
            var chest = _catalog.Config.KeyChests.FirstOrDefault(c => c.LootboxKey == request.ChestKey)
                ?? throw new EntityNotFoundException("Key chest", request.ChestKey);

            // Валідатор конфіга гарантує таблицю для кожної ключової скрині
            var box = _catalog.Lootboxes[chest.LootboxKey];
            var now = _timeProvider.GetUtcNow().UtcDateTime;

            if (request.Free)
                await TakeFreeAsync(request.PlayerId, chest.LootboxKey, chest.FreePerDay, chest.FreeCooldownMinutes, now,
                    cancellationToken);
            else
                await SpendKeysAsync(request.PlayerId, chest.KeyItemKey, request.Count, cancellationToken);

            var drops = await _opener.OpenAsync(request.PlayerId, box, request.Count, $"keychest:{box.Key}", now, cancellationToken);

            await _unitOfWork.SaveChangesAsync(cancellationToken);

            _logger.LogInformation("Player {PlayerId} opened {Count} × {Chest} ({Payment}): {Drops}",
                request.PlayerId, request.Count, box.Key, request.Free ? "free" : "keys",
                string.Join(", ", drops.Select(d => $"{d.Type}:{d.Key}×{d.Amount}")));

            return new LootboxOpening(box.Key, drops);
        }

        private async Task TakeFreeAsync(Guid playerId, string chestKey, int perDay, int cooldownMinutes, DateTime now,
            CancellationToken cancellationToken)
        {
            var progress = await _lootboxes.GetKeyChestProgressAsync(playerId, chestKey, cancellationToken);

            if (progress is null)
            {
                progress = new KeyChestProgress(Guid.NewGuid(), playerId, chestKey);
                await _lootboxes.AddKeyChestProgressAsync(progress, cancellationToken);
            }

            progress.UseFree(perDay, cooldownMinutes, now);
        }

        private async Task SpendKeysAsync(Guid playerId, string keyItemKey, int count, CancellationToken cancellationToken)
        {
            var stack = await _inventory.GetItemAsync(playerId, keyItemKey, cancellationToken);

            if (stack is null || stack.Count < count)
                throw new RequirementNotMetException(RefusalReasons.ItemNotEnough,
                    $"Not enough '{keyItemKey}': {stack?.Count ?? 0} of {count}.",
                    _catalog.Item(keyItemKey).DisplayName, count, stack?.Count ?? 0);

            stack.Consume(count);

            if (stack.Count == 0)
                _inventory.RemoveItem(stack);
        }
    }
}
