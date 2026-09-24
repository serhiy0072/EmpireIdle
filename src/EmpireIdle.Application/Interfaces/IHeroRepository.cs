using EmpireIdle.Domain.Entities;

namespace EmpireIdle.Application.Interfaces
{
    /// <summary>Репозиторій героїв гравця.</summary>
    public interface IHeroRepository
    {
        /// <summary>Увесь ростер гравця.</summary>
        Task<List<Hero>> GetByPlayerAsync(Guid playerId, CancellationToken cancellationToken = default);

        /// <summary>
        /// Конкретний герой за типом; null — такого в ростері немає.
        /// Використовується видачею, щоб відрізнити нового героя від дубліката.
        /// </summary>
        Task<Hero?> GetByKeyAsync(Guid playerId, string heroKey, CancellationToken cancellationToken = default);

        Task<Hero?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);

        /// <summary>Кілька героїв одним запитом — для вітрини ринку, лише читання.</summary>
        Task<List<Hero>> GetByIdsReadOnlyAsync(IReadOnlyCollection<Guid> ids, CancellationToken cancellationToken = default);

        /// <summary>
        /// Скільки героїв у ростері. Окремим запитом, бо кап маршів
        /// рахується на кожній відправці, а сам ростер там не потрібен.
        /// </summary>
        Task<int> CountAsync(Guid playerId, CancellationToken cancellationToken = default);

        /// <summary>
        /// Скільки героїв гравця стоять у цьому гарнізоні й готові виступити.
        /// Гарнізон у ключі навмисно: герой, що стоїть підкріпленням
        /// у союзника, вести похід із дому не може.
        /// </summary>
        Task<int> CountAvailableAsync(Guid playerId, Guid garrisonId, CancellationToken cancellationToken = default);

        /// <summary>
        /// Хто стоїть у гарнізоні, включно з чужими підкріпленнями.
        /// Екран оборони й бойова формула читають одне й те саме.
        /// </summary>
        Task<List<Hero>> GetByGarrisonAsync(Guid garrisonId, CancellationToken cancellationToken = default);

        /// <summary>
        /// Лідер цього гравця в цьому гарнізоні; null — слот вільний.
        /// </summary>
        Task<Hero?> GetLeaderAsync(Guid garrisonId, Guid playerId, CancellationToken cancellationToken = default);

        /// <summary>
        /// Чужі гарнізони, де стоять герої гравця. Потрібен відкликанню:
        /// герой без юнітів рядка в Reinforcements не лишає, і пошук
        /// контингенту по стеках його не знайшов би.
        /// </summary>
        Task<IReadOnlyList<Guid>> GetForeignGarrisonIdsAsync(Guid playerId, CancellationToken cancellationToken = default);

        Task AddAsync(Hero hero, CancellationToken cancellationToken = default);

        /// <summary>Накопичені уламки конкретного героя; null — ще жодного.</summary>
        Task<HeroShardProgress?> GetShardsAsync(Guid playerId, string heroKey, CancellationToken cancellationToken = default);

        Task AddShardsAsync(HeroShardProgress progress, CancellationToken cancellationToken = default);

        /// <summary>Активне замовлення на прокачку; null — черга вільна.</summary>
        Task<HeroLevelOrder?> GetActiveOrderAsync(Guid playerId, CancellationToken cancellationToken = default);

        /// <summary>
        /// Id дозрілих замовлень по світу. Сканерний запит: віддає лише
        /// ідентифікатори, бо кожне далі обробляється у власному scope.
        /// </summary>
        Task<IReadOnlyList<Guid>> GetIdsWithDueLevelUpAsync(DateTime utcNow, int batchSize,
            CancellationToken cancellationToken = default);

        /// <summary>
        /// Усі накопичені уламки гравця. Одним запитом, а не по ключу
        /// в циклі — ростер відкривається часто.
        /// </summary>
        Task<List<HeroShardProgress>> GetAllShardsAsync(Guid playerId, CancellationToken cancellationToken = default);

        Task<HeroLevelOrder?> GetOrderByIdAsync(Guid id, CancellationToken cancellationToken = default);

        Task AddOrderAsync(HeroLevelOrder order, CancellationToken cancellationToken = default);

        void RemoveOrder(HeroLevelOrder order);
    }
}
