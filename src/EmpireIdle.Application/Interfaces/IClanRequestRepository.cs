using EmpireIdle.Domain.Entities;
using EmpireIdle.Domain.Enums;

namespace EmpireIdle.Application.Interfaces
{
    /// <summary>Заявки на вступ і запрошення в клан.</summary>
    public interface IClanRequestRepository
    {
        Task<ClanRequest?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);

        /// <summary>
        /// Остання заявка гравця в цей клан цього виду, у будь-якому статусі.
        /// Потрібна і щоб не дублювати відкриту, і щоб перевірити кулдаун
        /// після відмови.
        /// </summary>
        Task<ClanRequest?> GetLatestAsync(Guid clanId, Guid playerId, ClanRequestKind kind,
            CancellationToken cancellationToken = default);

        /// <summary>Відкриті заявки клану — черга для офіцерів.</summary>
        Task<List<ClanRequest>> GetPendingByClanAsync(Guid clanId, ClanRequestKind kind, DateTime utcNow,
            CancellationToken cancellationToken = default);

        /// <summary>Відкриті запрошення гравцеві.</summary>
        Task<List<ClanRequest>> GetPendingByPlayerAsync(Guid playerId, ClanRequestKind kind, DateTime utcNow,
            CancellationToken cancellationToken = default);

        Task AddAsync(ClanRequest request, CancellationToken cancellationToken = default);

        /// <summary>
        /// Усі відкриті заявки й запрошення гравця, з трекінгом.
        /// Потрібні, щоб закрити їх, коли він кудись вступив.
        /// </summary>
        Task<List<ClanRequest>> GetPendingForPlayerAsync(Guid playerId, DateTime utcNow,
            CancellationToken cancellationToken = default);
    }
}
