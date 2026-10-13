using EmpireIdle.Application.Common.Security;
using EmpireIdle.Application.Interfaces;
using EmpireIdle.Application.Lootboxes.Contracts;
using EmpireIdle.Domain.Services;
using MediatR;

namespace EmpireIdle.Application.Lootboxes.Queries
{
    /// <summary>
    /// Ключові скрині гравця: скільки ключів у рюкзаку, скільки безкоштовних лишилось на добу
    /// й коли наступне — клієнт сам веде таймер до цього моменту.
    /// </summary>
    public record GetKeyChestsQuery(Guid PlayerId) : IRequest<IReadOnlyList<KeyChestView>>, IPlayerScopedRequest;

    internal sealed class GetKeyChestsQueryHandler : IRequestHandler<GetKeyChestsQuery, IReadOnlyList<KeyChestView>>
    {
        private readonly ILootboxRepository _lootboxes;
        private readonly IInventoryRepository _inventory;
        private readonly GameCatalog _catalog;
        private readonly TimeProvider _timeProvider;

        public GetKeyChestsQueryHandler(ILootboxRepository lootboxes, IInventoryRepository inventory, GameCatalog catalog,
            TimeProvider timeProvider)
        {
            _lootboxes = lootboxes;
            _inventory = inventory;
            _catalog = catalog;
            _timeProvider = timeProvider;
        }

        public async Task<IReadOnlyList<KeyChestView>> Handle(GetKeyChestsQuery request, CancellationToken cancellationToken)
        {
            var now = _timeProvider.GetUtcNow().UtcDateTime;

            var progress = (await _lootboxes.GetKeyChestProgressesReadOnlyAsync(request.PlayerId, cancellationToken))
                .ToDictionary(p => p.ChestKey);

            var keys = (await _inventory.GetItemsAsync(request.PlayerId, cancellationToken))
                .ToDictionary(i => i.ItemKey, i => i.Count);

            return _catalog.Config.KeyChests
                .Select(chest =>
                {
                    var own = progress.GetValueOrDefault(chest.LootboxKey);
                    var used = own?.FreeUsedToday(now) ?? 0;

                    return new KeyChestView(
                        chest.LootboxKey,
                        chest.KeyItemKey,
                        keys.GetValueOrDefault(chest.KeyItemKey),
                        chest.FreePerDay,
                        Math.Max(0, chest.FreePerDay - used),
                        own?.NextFreeAt(chest.FreePerDay, chest.FreeCooldownMinutes, now));
                })
                .ToList();
        }
    }
}
