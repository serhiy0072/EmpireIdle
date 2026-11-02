using EmpireIdle.Application.Interfaces;
using EmpireIdle.Domain.Services;

namespace EmpireIdle.Application.Wallets.Services
{
    /// <summary>
    /// VIP гравця світу: гаманець акаунтний, тож рівень береться з гаманця його акаунта.
    /// Гравець без гаманця чи акаунта — нульовий рівень, а не помилка: VIP лише додає, нічого не забирає.
    /// </summary>
    public class PlayerVip
    {
        private readonly IPlayerRepository _players;
        private readonly IPlayerWalletRepository _wallets;
        private readonly VipProgression _vip;

        public PlayerVip(IPlayerRepository players, IPlayerWalletRepository wallets, VipProgression vip)
        {
            _players = players;
            _wallets = wallets;
            _vip = vip;
        }

        /// <summary>Марші понад базові, які дає VIP-рівень гравця.</summary>
        public async Task<int> ExtraMarchesAsync(Guid playerId, CancellationToken cancellationToken)
        {
            var player = await _players.GetByIdAsync(playerId, cancellationToken);
            if (player is null)
                return 0;

            var wallet = await _wallets.GetByUserIdAsync(player.UserId, cancellationToken);

            return wallet is null ? 0 : _vip.ExtraMarches(_vip.Level(wallet.PurchasedGems));
        }
    }
}
