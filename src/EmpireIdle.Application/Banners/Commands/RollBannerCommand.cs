using EmpireIdle.Application.Common.Security;
using EmpireIdle.Application.Interfaces;
using EmpireIdle.Application.Rewards;
using EmpireIdle.Domain.Entities;
using EmpireIdle.Domain.Enums;
using EmpireIdle.Domain.Exceptions;
using EmpireIdle.Domain.Services;
using EmpireIdle.Domain.ValueObjects;
using MediatR;
using Microsoft.Extensions.Logging;

namespace EmpireIdle.Application.Banners.Commands
{
    /// <summary>
    /// Серія роллів банера за gems — від одного до MaxCount за один запит.
    /// Одна команда на серію, а не десять запитів: гравець не впирається
    /// в ліміт запитів, а pity й гаманець змінюються однією транзакцією.
    /// </summary>
    public record RollBannerCommand(Guid PlayerId, string BannerKey, int Count = 1, BannerCurrency Currency = BannerCurrency.Gems)
        : IRequest<BannerRollResponse>, IPlayerScopedRequest, IIdempotentRequest
    {
        public const int MaxCount = 10;
    }

    /// <summary>Чим платити за ролл: gems або печатками призову.</summary>
    public enum BannerCurrency
    {
        Gems = 0,
        Seals = 1
    }

    /// <param name="Drops">Випади в порядку роллів.</param>
    /// <param name="GemBalance">Залишок після списання: клієнту не треба окремо перепитувати гаманець.</param>
    public record BannerRollResponse(
        string BannerKey,
        IReadOnlyList<BannerDropResult> Drops,
        int RareSince,
        int UniqueSince,
        int GemBalance,
        int SealBalance);

    public record BannerDropResult(
        string DropKey,
        string DisplayName,
        Rarity Rarity,
        bool WasPity,
        bool LostFiftyFifty);

    internal sealed class RollBannerCommandHandler : IRequestHandler<RollBannerCommand, BannerRollResponse>
    {
        private readonly IPlayerRepository _playerRepository;
        private readonly IPlayerWalletRepository _walletRepository;
        private readonly IBannerRepository _banners;
        private readonly BannerRoller _roller;
        private readonly RewardDispatcher _dispatcher;
        private readonly IRandomSource _random;
        private readonly IServerContext _serverContext;
        private readonly IUnitOfWork _unitOfWork;
        private readonly TimeProvider _timeProvider;
        private readonly ILogger<RollBannerCommandHandler> _logger;

        public RollBannerCommandHandler(
            IPlayerRepository playerRepository,
            IPlayerWalletRepository walletRepository,
            IBannerRepository banners,
            BannerRoller roller,
            RewardDispatcher dispatcher,
            IRandomSource random,
            IServerContext serverContext,
            IUnitOfWork unitOfWork,
            TimeProvider timeProvider,
            ILogger<RollBannerCommandHandler> logger)
        {
            _playerRepository = playerRepository;
            _walletRepository = walletRepository;
            _banners = banners;
            _roller = roller;
            _dispatcher = dispatcher;
            _random = random;
            _serverContext = serverContext;
            _unitOfWork = unitOfWork;
            _timeProvider = timeProvider;
            _logger = logger;
        }

        public async Task<BannerRollResponse> Handle(RollBannerCommand request, CancellationToken cancellationToken)
        {
            var now = _timeProvider.GetUtcNow();

            var banner = _roller.FindBanner(request.BannerKey)
                ?? throw new EntityNotFoundException("Banner", request.BannerKey);

            if (banner.StartsAt is { } start && now < start)
                throw new RequirementNotMetException($"Banner '{banner.Key}' opens at {start:u}.");

            if (banner.EndsAt is { } end && now >= end)
                throw new RequirementNotMetException($"Banner '{banner.Key}' closed at {end:u}.");

            // Гаманець належить акаунту, тож ідемо через Player за UserId
            var player = await _playerRepository.GetByIdAsync(request.PlayerId, cancellationToken)
                ?? throw new EntityNotFoundException("Player", request.PlayerId);

            var wallet = await _walletRepository.GetByUserIdAsync(player.UserId, cancellationToken)
                ?? throw new EntityNotFoundException("Wallet", player.UserId);

            // Перевірка до списання — на всю серію одразу: половина серії за
            // рештки валюти була б несподіванкою, а Subtract кинув би виняток рівня
            // value object без цифр для гравця
            var paySeals = request.Currency == BannerCurrency.Seals;
            var unitPrice = paySeals ? banner.PriceSeals : banner.PriceGems;

            if (unitPrice <= 0)
                throw new RequirementNotMetException($"Banner '{banner.Key}' cannot be rolled with {request.Currency}.");

            var totalPrice = unitPrice * request.Count;

            if (paySeals && wallet.SealBalance < totalPrice)
                throw new NotEnoughResourcesException("seals", totalPrice, wallet.SealBalance);

            if (!paySeals && wallet.GemBalance.Value < totalPrice)
                throw new NotEnoughResourcesException("gems", totalPrice, wallet.GemBalance.Value);

            var progress = await _banners.GetPityAsync(request.PlayerId, banner.PityGroup, cancellationToken);

            if (progress is null)
            {
                progress = new BannerPityProgress(Guid.NewGuid(), request.PlayerId, banner.PityGroup);
                await _banners.AddPityAsync(progress, cancellationToken);
            }

            var utcNow = now.UtcDateTime;
            var drops = new List<BannerDropResult>(request.Count);

            // Кожен ролл серії бачить pity після попереднього: гарантія
            // спрацьовує посеред серії так само, як і при окремих запитах
            for (var i = 0; i < request.Count; i++)
            {
                var before = progress.State;

                // Сід окремо від ролла: він іде в журнал і дозволяє переграти роздачу
                var seed = _random.Next(int.MaxValue);
                var roll = _roller.Roll(banner, before, seed);

                if (paySeals)
                    wallet.SpendSeals(unitPrice, $"banner:{banner.Key}", utcNow);
                else
                    wallet.SpendGems(new GemAmount(unitPrice), $"banner:{banner.Key}", request.PlayerId, utcNow);

                progress.Apply(roll.State);

                await _banners.AddRollAsync(
                    new BannerRollRecord(
                        Guid.NewGuid(),
                        request.PlayerId,
                        _serverContext.ServerId,
                        banner.Key,
                        banner.PityGroup,
                        unitPrice,
                        seed,
                        before,
                        roll,
                        utcNow),
                    cancellationToken);

                await _dispatcher.GrantAllAsync(request.PlayerId, roll.Drop.Rewards, $"banner:{banner.Key}", utcNow, cancellationToken);

                drops.Add(new BannerDropResult(roll.Drop.Key, roll.Drop.DisplayName, roll.Drop.Rarity, roll.WasPity, roll.LostFiftyFifty));
            }

            // Одна транзакція на списання, стан pity, журнал і видачу всієї серії:
            // токен BannerPity робить другий паралельний ролл конфліктом, а не подвійною гарантією
            await _unitOfWork.SaveChangesAsync(cancellationToken);

            _logger.LogInformation(
                "Player {PlayerId} rolled {Count} times on banner {BannerKey}: {Drops}",
                request.PlayerId, request.Count, banner.Key, string.Join(", ", drops.Select(d => d.DropKey)));

            return new BannerRollResponse(
                banner.Key,
                drops,
                progress.RareSince,
                progress.UniqueSince,
                wallet.GemBalance.Value,
                wallet.SealBalance);
        }
    }
}
