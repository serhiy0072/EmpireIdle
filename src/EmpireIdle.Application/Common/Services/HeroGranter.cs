using EmpireIdle.Application.Interfaces;
using EmpireIdle.Domain.Entities;
using EmpireIdle.Domain.Services;
using EmpireIdle.Domain.ValueObjects;

namespace EmpireIdle.Application.Common.Services
{
    /// <summary>
    /// Видає героя незалежно від джерела: квест, віха, призов за уламки,
    /// згодом банер.
    ///
    /// Правило одне на всіх — новий герой іде в ростер, дублікат у сузір'я,
    /// надлишок понад стелю в джеми. Тримається в одному місці навмисно:
    /// продубльоване, воно розійшлося б на першій же зміні балансу.
    /// </summary>
    public class HeroGranter
    {
        private readonly IHeroRepository _heroRepository;
        private readonly IPlayerRepository _playerRepository;
        private readonly IPlayerWalletRepository _walletRepository;
        private readonly IServerContext _serverContext;
        private readonly GameCatalog _catalog;

        public HeroGranter(
            IHeroRepository heroRepository,
            IPlayerRepository playerRepository,
            IPlayerWalletRepository walletRepository,
            IServerContext serverContext,
            GameCatalog catalog)
        {
            _heroRepository = heroRepository;
            _playerRepository = playerRepository;
            _walletRepository = walletRepository;
            _serverContext = serverContext;
            _catalog = catalog;
        }

        /// <param name="reference">Джерело — ключ квесту або призову. Іде в журнал гаманця.</param>
        public async Task GrantAsync(Guid playerId, string heroKey, string reference,
            DateTime utcNow, CancellationToken cancellationToken = default)
        {
            // Кидає, якщо героя немає в каталозі — краще впасти при видачі,
            // ніж створити рядок, для якого немає ні стат, ні картки
            var config = _catalog.FindHero(heroKey)
                ?? throw new InvalidOperationException($"Hero '{heroKey}' from '{reference}' is not in the catalog.");

            var existing = await _heroRepository.GetByKeyAsync(playerId, heroKey, cancellationToken);

            if (existing is null)
            {
                await _heroRepository.AddAsync(
                    new Hero(Guid.NewGuid(), playerId, _serverContext.ServerId, heroKey, utcNow),
                    cancellationToken);

                return;
            }

            var settings = _catalog.Config.HeroSettings;

            if (existing.TryAddConstellation(settings.MaxConstellation, utcNow))
                return;

            var gems = settings.OverflowGems.GetValueOrDefault(config.Rank.ToString(), 0);

            if (gems < 1)
                return;

            // Гаманець належить акаунту, а не гравцю — потрібен перехід через Player
            var player = await _playerRepository.GetByIdAsync(playerId, cancellationToken)
                ?? throw new InvalidOperationException($"Player {playerId} not found.");

            var wallet = await _walletRepository.GetByUserIdAsync(player.UserId, cancellationToken)
                ?? throw new InvalidOperationException($"Wallet not found for player {playerId}.");

            wallet.AddGems(new GemAmount(gems), $"hero-overflow:{heroKey}", playerId, utcNow);
        }
    }
}
