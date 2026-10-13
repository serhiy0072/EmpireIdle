using EmpireIdle.Application.Common.Security;
using EmpireIdle.Application.Interfaces;
using EmpireIdle.Domain.Exceptions;
using EmpireIdle.Domain.Services;
using EmpireIdle.Domain.ValueObjects;
using MediatR;
using Microsoft.Extensions.Logging;

namespace EmpireIdle.Application.Shop.Commands
{
    /// <summary>
    /// Купує шматки зброї звичайного чи рідкісного героя за gems (GDD §6.4, §9.12). Унікальні не продаються —
    /// їхні шматки лише зі скринь зброї. Шматки лягають на героя, рівень зброї гравець піднімає сам.
    /// Повертає новий баланс gems.
    /// </summary>
    public record BuyHeroWeaponShardsCommand(Guid PlayerId, Guid HeroId, int Count)
        : IRequest<int>, IPlayerScopedRequest, IIdempotentRequest;

    internal sealed class BuyHeroWeaponShardsCommandHandler : IRequestHandler<BuyHeroWeaponShardsCommand, int>
    {
        private readonly IHeroRepository _heroRepository;
        private readonly IPlayerRepository _playerRepository;
        private readonly IPlayerWalletRepository _walletRepository;
        private readonly GameCatalog _catalog;
        private readonly IUnitOfWork _unitOfWork;
        private readonly TimeProvider _timeProvider;
        private readonly ILogger<BuyHeroWeaponShardsCommandHandler> _logger;

        public BuyHeroWeaponShardsCommandHandler(
            IHeroRepository heroRepository,
            IPlayerRepository playerRepository,
            IPlayerWalletRepository walletRepository,
            GameCatalog catalog,
            IUnitOfWork unitOfWork,
            TimeProvider timeProvider,
            ILogger<BuyHeroWeaponShardsCommandHandler> logger)
        {
            _heroRepository = heroRepository;
            _playerRepository = playerRepository;
            _walletRepository = walletRepository;
            _catalog = catalog;
            _unitOfWork = unitOfWork;
            _timeProvider = timeProvider;
            _logger = logger;
        }

        public async Task<int> Handle(BuyHeroWeaponShardsCommand request, CancellationToken cancellationToken)
        {
            var now = _timeProvider.GetUtcNow().UtcDateTime;

            var hero = await _heroRepository.GetByIdAsync(request.HeroId, cancellationToken)
                ?? throw new EntityNotFoundException("Hero", request.HeroId.ToString());

            // Чужий герой не відрізняється від неіснуючого
            if (hero.PlayerId != request.PlayerId)
                throw new EntityNotFoundException("Hero", request.HeroId.ToString());

            var config = _catalog.FindHero(hero.HeroKey)
                ?? throw new EntityNotFoundException("Hero type", hero.HeroKey);

            if (!_catalog.Config.HeroSettings.WeaponShardPriceGems.TryGetValue(config.Rank, out var price))
                throw new RequirementNotMetException(RefusalReasons.HeroWeaponNotSold,
                    $"Weapon shards of '{hero.HeroKey}' are not sold.", config.DisplayName);

            var player = await _playerRepository.GetByIdAsync(request.PlayerId, cancellationToken)
                ?? throw new EntityNotFoundException("Player", request.PlayerId);

            // Гаманець належить акаунту, тож ідемо через Player за UserId
            var wallet = await _walletRepository.GetByUserIdAsync(player.UserId, cancellationToken)
                ?? throw new EntityNotFoundException("Wallet", player.UserId);

            var total = price * request.Count;

            // Перевірка до списання: гравцю потрібна відмова з цифрами, а не виняток value object
            if (wallet.GemBalance.Value < total)
                throw new NotEnoughResourcesException("gems", total, wallet.GemBalance.Value);

            wallet.SpendGems(new GemAmount(total), $"shop:weapon:{hero.HeroKey}", request.PlayerId, now);
            hero.AddWeaponShards(request.Count, now);

            await _unitOfWork.SaveChangesAsync(cancellationToken);

            _logger.LogInformation("Player {PlayerId} bought {Count} weapon shards of {HeroKey} for {Gems} gems",
                request.PlayerId, request.Count, hero.HeroKey, total);

            return wallet.GemBalance.Value;
        }
    }
}
