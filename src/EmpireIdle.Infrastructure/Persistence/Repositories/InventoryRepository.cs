using EmpireIdle.Application.Interfaces;
using EmpireIdle.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace EmpireIdle.Infrastructure.Persistence.Repositories
{
    /// <summary>Репозиторій інвентаря (EF Core).</summary>
    public class InventoryRepository : IInventoryRepository
    {
        private readonly AppDbContext _context;

        public InventoryRepository(AppDbContext context)
        {
            _context = context;
        }

        /// <inheritdoc/>
        public Task<List<PlayerItem>> GetItemsAsync(Guid playerId, CancellationToken cancellationToken = default)
            => _context.PlayerItems
            .AsNoTracking()
            .Where(i => i.PlayerId == playerId && i.Count > 0)
            .ToListAsync(cancellationToken);

        /// <inheritdoc/>
        /// <remarks>
        /// Спершу дивимось у поточну одиницю роботи: предмет, щойно доданий у цій же
        /// транзакції (10-ролл, «Забрати все»), база ще не бачить — і друга видача того
        /// самого ключа дала б другий INSERT на унікальний (PlayerId, ItemKey).
        /// </remarks>
        public async Task<PlayerItem?> GetItemAsync(Guid playerId, string itemKey, CancellationToken cancellationToken = default)
            => _context.PlayerItems.Local.FirstOrDefault(i => i.PlayerId == playerId && i.ItemKey == itemKey)
               ?? await _context.PlayerItems
                   .FirstOrDefaultAsync(i => i.PlayerId == playerId && i.ItemKey == itemKey, cancellationToken);

        /// <inheritdoc/>
        public Task<List<EquipmentItem>> GetEquipmentAsync(Guid playerId, CancellationToken cancellationToken = default)
            => _context.EquipmentItems
            .AsNoTracking()
            .Include(e => e.Stats)
            .AsSplitQuery()
            .Where(e => e.PlayerId == playerId)
            .ToListAsync(cancellationToken);

        /// <inheritdoc/>
        public Task<EquipmentItem?> GetEquipmentByIdAsync(Guid id, CancellationToken cancellationToken = default)
            => _context.EquipmentItems
            .Include(e => e.Stats)
            .AsSplitQuery()
            .FirstOrDefaultAsync(e => e.Id == id, cancellationToken);

        public Task<List<EquipmentItem>> GetEquipmentByIdsReadOnlyAsync(IReadOnlyCollection<Guid> ids,
            CancellationToken cancellationToken = default)
            => _context.EquipmentItems
            .AsNoTracking()
            .Include(e => e.Stats)
            .AsSplitQuery()
            .Where(e => ids.Contains(e.Id))
            .ToListAsync(cancellationToken);

        /// <inheritdoc/>
        public Task<List<EquipmentItem>> GetEquippedAsync(Guid heroId, CancellationToken cancellationToken = default)
            => _context.EquipmentItems
            .Include(e => e.Stats)
            .AsSplitQuery()
            .Where(e => e.EquippedByHeroId == heroId)
            .ToListAsync(cancellationToken);

        /// <inheritdoc/>
        public Task<List<EquipmentItem>> GetEquippedByHeroesAsync(IReadOnlyCollection<Guid> heroIds,
            CancellationToken cancellationToken = default)
            => _context.EquipmentItems
            .Include(e => e.Stats)
            .AsSplitQuery()
            .Where(e => e.EquippedByHeroId != null && heroIds.Contains(e.EquippedByHeroId.Value))
            .ToListAsync(cancellationToken);

        /// <inheritdoc/>
        public async Task AddItemAsync(PlayerItem item, CancellationToken cancellationToken = default)
        {
            await _context.PlayerItems.AddAsync(item, cancellationToken);
        }

        /// <inheritdoc/>
        public async Task AddEquipmentAsync(EquipmentItem equipment, CancellationToken cancellationToken = default)
        {
            await _context.EquipmentItems.AddAsync(equipment, cancellationToken);
        }

        /// <inheritdoc/>
        public void RemoveItem(PlayerItem item) => _context.PlayerItems.Remove(item);

        public void RemoveEquipment(EquipmentItem equipment) => _context.EquipmentItems.Remove(equipment);
    }
}
