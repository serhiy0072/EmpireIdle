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

        /// <summary>
        /// Скільки героїв у ростері. Окремим запитом, бо кап маршів
        /// рахується на кожній відправці, а сам ростер там не потрібен.
        /// </summary>
        Task<int> CountAsync(Guid playerId, CancellationToken cancellationToken = default);

        /// <summary>
        /// Скільки героїв гравця готові вийти в марш. Кап рахується з них,
        /// а не з усього ростера: поранений і вже відправлений слот не дають.
        /// </summary>
        Task<int> CountAvailableAsync(Guid playerId, CancellationToken cancellationToken = default);

        /// <summary>
        /// Хто стоїть у гарнізоні, включно з чужими підкріпленнями.
        /// Екран оборони й бойова формула читають одне й те саме.
        /// </summary>
        Task<List<Hero>> GetByGarrisonAsync(Guid garrisonId, CancellationToken cancellationToken = default);

        /// <summary>
        /// Лідер цього гравця в цьому гарнізоні; null — слот вільний.
        /// </summary>
        Task<Hero?> GetLeaderAsync(Guid garrisonId, Guid playerId, CancellationToken cancellationToken = default);

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
