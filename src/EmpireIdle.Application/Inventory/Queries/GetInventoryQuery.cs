using EmpireIdle.Application.Common.Security;
using EmpireIdle.Application.Interfaces;
using EmpireIdle.Application.Inventory.ReadModels;
using EmpireIdle.Domain.Enums;
using EmpireIdle.Domain.Services;
using MediatR;

namespace EmpireIdle.Application.Inventory.Queries
{
    /// <summary>Запит на інвентар гравця.</summary>
    public record GetInventoryQuery(Guid PlayerId) : IRequest<InventoryView>, IPlayerScopedRequest;

    public sealed class GetInventoryQueryHandler : IRequestHandler<GetInventoryQuery, InventoryView>
    {
        private readonly IInventoryRepository _repository;
        private readonly IActiveEffectRepository _effectRepository;
        private readonly GameCatalog _catalog;
        private readonly TimeProvider _timeProvider;

        public GetInventoryQueryHandler(IInventoryRepository repository, IActiveEffectRepository effectRepository,
            GameCatalog catalog, TimeProvider timeProvider)
        {
            _repository = repository;
            _effectRepository = effectRepository;
            _catalog = catalog;
            _timeProvider = timeProvider;
        }

        public async Task<InventoryView> Handle(GetInventoryQuery request, CancellationToken cancellationToken)
        {
            var items = await _repository.GetItemsAsync(request.PlayerId, cancellationToken);
            var equipment = await _repository.GetEquipmentAsync(request.PlayerId, cancellationToken);

            // Прострочені відсіюємо тут: фонове очищення не гарантує миттєвості
            var now = _timeProvider.GetUtcNow().UtcDateTime;
            var effects = (await _effectRepository.GetByPlayerAsync(request.PlayerId, cancellationToken))
                .Where(e => e.IsActive(now))
                .ToList();

            // Приріст заточки — з конфіга: криву балансують, і вона не властивість предмета
            var enhancementBonus = _catalog.Config.Equipment.EnhancementBonusPerLevel;

            return new InventoryView(
                items.Select(ItemView).ToList(),
                equipment
                    .Select(e => new EquipmentView(e.Id, e.ItemKey, e.Slot, e.Rarity, e.EnhancementLevel, e.EquippedByHeroId,
                        e.SlotIndex, e.IsBroken,
                        e.Stats.ToDictionary(s => s.StatKey, s => e.GetStatValue(s.StatKey, enhancementBonus)),
                        e.IsOnMarket, e.ResaleLockedUntil))
                    .ToList(),
                effects.Select(e => new ActiveEffectView(e.Target, e.Multiplier, e.ExpiresAt, e.SourceItemKey)).ToList());
        }

        private InventoryItemView ItemView(Domain.Entities.PlayerItem item)
        {
            var config = _catalog.Items.GetValueOrDefault(item.ItemKey);

            return new InventoryItemView(item.ItemKey, config?.DisplayName ?? item.ItemKey, config?.Description ?? string.Empty,
                config?.Rarity ?? Rarity.Common, config?.Type ?? "unknown", item.Count);
        }
    }
}
