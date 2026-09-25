using EmpireIdle.Domain.Entities;

namespace EmpireIdle.Application.Interfaces
{
    /// <summary>Прогрес кланових квестів.</summary>
    public interface IClanQuestRepository
    {
        Task<ClanQuestProgress?> GetAsync(Guid clanId, string questKey, CancellationToken cancellationToken = default);

        Task<List<ClanQuestProgress>> GetByClanAsync(Guid clanId, CancellationToken cancellationToken = default);

        /// <summary>Ключі завершених квестів клану — від них залежать відкриті слоти споруд.</summary>
        Task<List<string>> GetCompletedKeysAsync(Guid clanId, CancellationToken cancellationToken = default);

        Task AddAsync(ClanQuestProgress progress, CancellationToken cancellationToken = default);
    }
}
