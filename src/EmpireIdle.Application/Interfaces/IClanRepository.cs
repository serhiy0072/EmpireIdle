using EmpireIdle.Domain.Entities;

namespace EmpireIdle.Application.Interfaces
{
    /// <summary>Репозиторій кланів. Клан завантажується цілком — зі складом і ролями.</summary>
    public interface IClanRepository
    {
        /// <summary>Клан зі складом і ролями; null — немає.</summary>
        Task<Clan?> GetByIdAsync(Guid clanId, CancellationToken cancellationToken = default);

        /// <summary>
        /// Клан, у якому перебуває гравець; null — без клану.
        /// Іде через ClanMembers, а не через Player.ClanId: денормалізація
        /// придатна для перевірок, але не для завантаження агрегату.
        /// </summary>
        Task<Clan?> GetByMemberAsync(Guid playerId, CancellationToken cancellationToken = default);

        /// <summary>Чи зайнята назва або тег у поточному світі.</summary>
        Task<bool> ExistsAsync(string name, string tag, CancellationToken cancellationToken = default);

        Task AddAsync(Clan clan, CancellationToken cancellationToken = default);

        void Remove(Clan clan);

        /// <summary>
        /// Сторінка кланів світу з пошуком за назвою або тегом, без регістру.
        /// Total — усього збігів, для пагінації.
        /// </summary>
        Task<(IReadOnlyList<ClanCard> Items, int Total)> BrowseAsync(string? search, int skip, int take,
            CancellationToken cancellationToken = default);

        /// <summary>Картка одного клану; null — немає.</summary>
        Task<ClanCard?> GetCardAsync(Guid clanId, CancellationToken cancellationToken = default);

        /// <summary>Id клану гравця; null — без клану. Для читань, яким склад не потрібен.</summary>
        Task<Guid?> GetClanIdByMemberAsync(Guid playerId, CancellationToken cancellationToken = default);

        /// <summary>Картки кількох кланів одним запитом — для списку запрошень.</summary>
        Task<Dictionary<Guid, ClanCard>> GetCardsAsync(IReadOnlyCollection<Guid> clanIds,
            CancellationToken cancellationToken = default);
    }
}
