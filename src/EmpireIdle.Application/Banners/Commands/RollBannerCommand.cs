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
    /// <summary>Один ролл банера за gems.</summary>
    public record RollBannerCommand(Guid PlayerId, string BannerKey)
        : IRequest<BannerRollResponse>, IPlayerScopedRequest, IIdempotentRequest;

    /// <param name="GemBalance">Залишок після списання: клієнту не треба окремо перепитувати гаманець.</param>
    public record BannerRollResponse(
        string BannerKey,
        string DropKey,
        string DisplayName,
        Rarity Rarity,
        bool WasPity,
        bool LostFiftyFifty,
        int RareSince,
        int UniqueSince,
        int GemBalance);

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

            // Перевірка до списання: Subtract кинув би виняток рівня value object,
            // а гравцю потрібна відмова з цифрами
            if (wallet.GemBalance.Value < banner.PriceGems)
                throw new NotEnoughResourcesException("gems", banner.PriceGems, wallet.GemBalance.Value);

            var progress = await _banners.GetPityAsync(request.PlayerId, banner.PityGroup, cancellationToken);

            if (progress is null)
            {
                progress = new BannerPityProgress(Guid.NewGuid(), request.PlayerId, banner.PityGroup);
                await _banners.AddPityAsync(progress, cancellationToken);
            }

            var utcNow = now.UtcDateTime;
            var before = progress.State;

            // Сід окремо від ролла: він іде в журнал і дозволяє переграти роздачу
            var seed = _random.Next(int.MaxValue);
            var roll = _roller.Roll(banner, before, seed);

            wallet.SpendGems(new GemAmount(banner.PriceGems), $"banner:{banner.Key}", request.PlayerId, utcNow);
            progress.Apply(roll.State);

            await _banners.AddRollAsync(
                new BannerRollRecord(
                    Guid.NewGuid(),
                    request.PlayerId,
                    _serverContext.ServerId,
                    banner.Key,
                    banner.PityGroup,
                    banner.PriceGems,
                    seed,
                    before,
                    roll,
                    utcNow),
                cancellationToken);

            await _dispatcher.GrantAllAsync(request.PlayerId, roll.Drop.Rewards, $"banner:{banner.Key}", utcNow, cancellationToken);

            // Одна транзакція на списання, стан pity, журнал і видачу:
            // токен BannerPity робить другий паралельний ролл конфліктом, а не подвійною гарантією
            await _unitOfWork.SaveChangesAsync(cancellationToken);

            _logger.LogInformation(
                "Player {PlayerId} rolled {DropKey} ({Rarity}) on banner {BannerKey}, pity {WasPity}",
                request.PlayerId, roll.Drop.Key, roll.Drop.Rarity, banner.Key, roll.WasPity);

            return new BannerRollResponse(
                banner.Key,
                roll.Drop.Key,
                roll.Drop.DisplayName,
                roll.Drop.Rarity,
                roll.WasPity,
                roll.LostFiftyFifty,
                progress.RareSince,
                progress.UniqueSince,
                wallet.GemBalance.Value);
        }
    }
}
