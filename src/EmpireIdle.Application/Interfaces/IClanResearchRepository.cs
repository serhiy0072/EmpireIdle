using EmpireIdle.Domain.Entities;

namespace EmpireIdle.Application.Interfaces
{
    /// <summary>Кланові технології (рішення 28.10.2026).</summary>
    public interface IClanResearchRepository
    {
        /// <summary>Технології клану для зміни або null, якщо клан ще нічого не вивчав.</summary>
        Task<ClanResearch?> GetByClanAsync(Guid clanId, CancellationToken cancellationToken = default);

        /// <summary>Вивчені рівні клану лише для читання; клан нічого не вивчив — null.</summary>
        Task<IReadOnlyDictionary<string, int>?> GetLevelsByClanAsync(Guid clanId, CancellationToken cancellationToken = default);

        /// <summary>
        /// Вивчені рівні клану, у якому гравець зараз; не в клані або клан нічого не вивчив — null.
        /// Читає збережений стан: зміни цієї ж команди до SaveChanges сюди ще не потрапляють.
        /// </summary>
        Task<IReadOnlyDictionary<string, int>?> GetLevelsByMemberAsync(Guid playerId, CancellationToken cancellationToken = default);

        Task AddAsync(ClanResearch research, CancellationToken cancellationToken = default);

        /// <summary>Прибирає технології розпущеного клану; повертає кількість рядків.</summary>
        Task<int> RemoveByClanAsync(Guid clanId, CancellationToken cancellationToken = default);
    }
}
