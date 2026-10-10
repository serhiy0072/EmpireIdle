using EmpireIdle.Domain.Entities;

namespace EmpireIdle.Application.Interfaces
{
    /// <summary>Лічильники гарантій скринь.</summary>
    public interface ILootboxRepository
    {
        /// <summary>Лічильник гравця в скрині. null — гравець її ще не відкривав.</summary>
        Task<LootboxPity?> GetPityAsync(Guid playerId, string lootboxKey, CancellationToken cancellationToken = default);

        /// <summary>Усі лічильники гравця — для показу «до гарантії лишилось».</summary>
        Task<List<LootboxPity>> GetPitiesReadOnlyAsync(Guid playerId, CancellationToken cancellationToken = default);

        Task AddPityAsync(LootboxPity pity, CancellationToken cancellationToken = default);
    }
}
