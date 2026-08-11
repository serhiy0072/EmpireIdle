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
            .Where(i => i.PlayerId == playerId && i.Count > 0)
            .ToListAsync(cancellationToken);

        /// <inheritdoc/>
        public Task<PlayerItem?> GetItemAsync(Guid playerId, string itemKey, CancellationToken cancellationToken = default)
            => _context.PlayerItems
            .FirstOrDefaultAsync(i => i.PlayerId == playerId && i.ItemKey == itemKey, cancellationToken);

        /// <inheritdoc/>
        public Task<List<EquipmentItem>> GetEquipmentAsync(Guid playerId, CancellationToken cancellationToken = default)
            => _context.EquipmentItems
            .Include(e => e.Stats)
            .Where(e => e.PlayerId == playerId)
            .ToListAsync(cancellationToken);

        /// <inheritdoc/>
        public Task<EquipmentItem?> GetEquipmentByIdAsync(Guid id, CancellationToken cancellationToken = default)
            => _context.EquipmentItems
            .Include(e => e.Stats)
            .FirstOrDefaultAsync(e => e.Id == id, cancellationToken);

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
    }
}