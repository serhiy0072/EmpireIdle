using EmpireIdle.Domain.Entities;

namespace EmpireIdle.Application.Interfaces
{
    /// <summary>Кланові технології (рішення 28.10.2026).</summary>
    public interface IClanResearchRepository
    {
        /// <summary>Технології клану для зміни або null, якщо клан ще нічого не вивчав.</summary>
        Task<ClanResearch?> GetByClanAsync(Guid clanId, CancellationToken cancellationToken = default);

        Task AddAsync(ClanResearch research, CancellationToken cancellationToken = default);

        /// <summary>Прибирає технології розпущеного клану; повертає кількість рядків.</summary>
        Task<int> RemoveByClanAsync(Guid clanId, CancellationToken cancellationToken = default);
    }
}
