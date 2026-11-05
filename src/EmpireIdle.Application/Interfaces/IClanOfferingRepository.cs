using EmpireIdle.Domain.Entities;

namespace EmpireIdle.Application.Interfaces
{
    /// <summary>Особисті книги підношень і внески в клани (рішення 03.11).</summary>
    public interface IClanOfferingRepository
    {
        Task<ClanOfferingLedger?> GetLedgerAsync(Guid playerId, CancellationToken cancellationToken = default);

        /// <summary>Особиста книга для екрана, без відстеження; null — гравець ще нічого не підносив і не забирав.</summary>
        Task<ClanOfferingLedger?> GetLedgerReadOnlyAsync(Guid playerId, CancellationToken cancellationToken = default);

        Task AddLedgerAsync(ClanOfferingLedger ledger, CancellationToken cancellationToken = default);

        /// <summary>Внески нинішніх учасників клану для рейтингу: той, хто пішов, зі списку зникає, але рядок лишається.</summary>
        Task<IReadOnlyList<ClanContribution>> GetMemberContributionsAsync(Guid clanId, CancellationToken cancellationToken = default);

        Task<ClanContribution?> GetContributionAsync(Guid clanId, Guid playerId, CancellationToken cancellationToken = default);

        Task AddContributionAsync(ClanContribution contribution, CancellationToken cancellationToken = default);

        /// <summary>Прибирає внески в розпущений клан; особисті книги лишаються. Повертає кількість рядків.</summary>
        Task<int> RemoveContributionsByClanAsync(Guid clanId, CancellationToken cancellationToken = default);
    }
}
