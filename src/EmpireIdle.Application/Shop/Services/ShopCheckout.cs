using EmpireIdle.Application.Interfaces;
using EmpireIdle.Domain.Entities;
using EmpireIdle.Domain.Exceptions;
using EmpireIdle.Domain.Services;
using EmpireIdle.Domain.Services.Config;
using EmpireIdle.Domain.ValueObjects;

namespace EmpireIdle.Application.Shop.Services
{
    /// <summary>Пропозиція крамниці й гаманець, з якого за неї вже списано.</summary>
    public record ShopCharge(ShopItemConfig Offer, ItemConfig Item, PlayerWallet Wallet, string Reference);

    /// <summary>
    /// Каса крамниці: пропозиція, ліміт на покупку, вікно продажу й списання gems. Спільна для
    /// покупки в рюкзак і покупки скрині з відкриттям — що видати, вирішує команда.
    /// </summary>
    public sealed class ShopCheckout
    {
        private readonly IPlayerRepository _playerRepository;
        private readonly IPlayerWalletRepository _walletRepository;
        private readonly IServerRepository _serverRepository;
        private readonly IServerContext _serverContext;
        private readonly GameCatalog _catalog;

        public ShopCheckout(
            IPlayerRepository playerRepository,
            IPlayerWalletRepository walletRepository,
            IServerRepository serverRepository,
            IServerContext serverContext,
            GameCatalog catalog)
        {
            _playerRepository = playerRepository;
            _walletRepository = walletRepository;
            _serverRepository = serverRepository;
            _serverContext = serverContext;
            _catalog = catalog;
        }

        /// <summary>Пропозиція за ключем від гравця; кидає 404, якщо такої немає.</summary>
        public (ShopItemConfig Offer, ItemConfig Item) Find(string itemKey)
        {
            var offer = _catalog.Config.Shop.Items.FirstOrDefault(i => i.ItemKey == itemKey)
                ?? throw new EntityNotFoundException("Shop item", itemKey);

            // Валідатор конфіга гарантує, що кожен товар є в Items
            return (offer, _catalog.Item(offer.ItemKey));
        }

        /// <summary>Перевіряє пропозицію й списує її ціну за count штук.</summary>
        public async Task<ShopCharge> ChargeAsync(Guid playerId, ShopItemConfig offer, ItemConfig item, int count,
            DateTime utcNow, CancellationToken cancellationToken)
        {
            if (count > offer.MaxPerPurchase)
                throw new RequirementNotMetException(RefusalReasons.ShopMaxPerPurchase,
                    $"At most {offer.MaxPerPurchase} × '{offer.ItemKey}' per purchase.", offer.MaxPerPurchase);

            // Вікно перевіряє сервер, а не лише вітрина: клієнт міг тримати сторінку відкритою після кінця вікна
            if (offer.ServerLevel is not null)
            {
                var server = await _serverRepository.GetByIdAsync(_serverContext.ServerId, cancellationToken)
                    ?? throw new InvalidOperationException($"Server {_serverContext.ServerId} not found.");

                if (!offer.IsOnSaleAt(server.Level, server.LevelSince, utcNow))
                    throw new RequirementNotMetException(RefusalReasons.ShopOfferClosed, $"'{offer.ItemKey}' is not on sale now.");
            }

            var player = await _playerRepository.GetByIdAsync(playerId, cancellationToken)
                ?? throw new EntityNotFoundException("Player", playerId);

            // Гаманець належить акаунту, тож ідемо через Player за UserId
            var wallet = await _walletRepository.GetByUserIdAsync(player.UserId, cancellationToken)
                ?? throw new EntityNotFoundException("Wallet", player.UserId);

            var total = offer.PriceGems * count;

            // Перевірка до списання: гравцю потрібна відмова з цифрами, а не виняток value object
            if (wallet.GemBalance.Value < total)
                throw new NotEnoughResourcesException("gems", total, wallet.GemBalance.Value);

            var reference = $"shop:{offer.ItemKey}";

            wallet.SpendGems(new GemAmount(total), reference, playerId, utcNow);

            return new ShopCharge(offer, item, wallet, reference);
        }
    }
}
