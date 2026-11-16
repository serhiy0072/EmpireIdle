using EmpireIdle.Application.Interfaces;
using EmpireIdle.Domain.Entities;

namespace EmpireIdle.Application.Common
{
    /// <summary>
    /// Село, гарнізон і гаманець гравця існують від реєстрації й нікуди не зникають. Якщо їх немає —
    /// це поломка даних, а не «не знайдено»: відповідь має бути 500 зі слідом у логах, а не 404, який
    /// клієнт показав би гравцю як звичайну відмову. Одне місце на всі обробники — щоб тип винятку
    /// й текст не розходились від файлу до файлу.
    ///
    /// Сутності, які називає сам запит (герой, клан, заявка, предмет), сюди не належать:
    /// їх може не бути чесно, і для них лишається EntityNotFoundException.
    /// </summary>
    public static class RequiredAggregates
    {
        public static async Task<Village> RequireByPlayerAsync(this IVillageRepository villages, Guid playerId,
            CancellationToken cancellationToken)
            => await villages.GetByPlayerIdAsync(playerId, cancellationToken) ?? throw MissingVillage(playerId);

        public static async Task<Village> RequireByPlayerReadOnlyAsync(this IVillageRepository villages, Guid playerId,
            CancellationToken cancellationToken)
            => await villages.GetByPlayerIdReadOnlyAsync(playerId, cancellationToken) ?? throw MissingVillage(playerId);

        public static async Task<Garrison> RequireByVillageAsync(this IGarrisonRepository garrisons, Guid villageId,
            CancellationToken cancellationToken)
            => await garrisons.GetByVillageIdAsync(villageId, cancellationToken) ?? throw MissingGarrison(villageId);

        public static async Task<Garrison> RequireByVillageReadOnlyAsync(this IGarrisonRepository garrisons, Guid villageId,
            CancellationToken cancellationToken)
            => await garrisons.GetByVillageIdReadOnlyAsync(villageId, cancellationToken) ?? throw MissingGarrison(villageId);

        public static async Task<PlayerWallet> RequireByUserAsync(this IPlayerWalletRepository wallets, string userId,
            CancellationToken cancellationToken)
            => await wallets.GetByUserIdAsync(userId, cancellationToken)
               ?? throw new InvalidOperationException($"Wallet not found for user {userId}.");

        private static InvalidOperationException MissingVillage(Guid playerId) => new($"Village not found for player {playerId}.");

        private static InvalidOperationException MissingGarrison(Guid villageId) => new($"Garrison not found for village {villageId}.");
    }
}
