using EmpireIdle.Application.Common.Security;
using EmpireIdle.Application.Interfaces;
using EmpireIdle.Domain.Entities;
using EmpireIdle.Domain.Enums;
using EmpireIdle.Domain.Exceptions;
using EmpireIdle.Domain.Services;
using EmpireIdle.Domain.Services.Config;
using MediatR;
using Microsoft.Extensions.Logging;

namespace EmpireIdle.Application.Heroes.Commands
{
    /// <summary>
    /// Купівля уламків героя за золото в залі героїв. Призов не відбувається
    /// автоматично — накопичене лежить, доки гравець сам не витратить його
    /// командою SummonHero.
    /// </summary>
    public record BuyHeroShardsCommand(Guid PlayerId, string HeroKey, int Count)
        : IRequest, IPlayerScopedRequest, IIdempotentRequest;

    internal sealed class BuyHeroShardsCommandHandler : IRequestHandler<BuyHeroShardsCommand>
    {
        private readonly IVillageRepository _villageRepository;
        private readonly IHeroRepository _heroRepository;
        private readonly IUnitOfWork _unitOfWork;
        private readonly TimeProvider _timeProvider;
        private readonly ILogger<BuyHeroShardsCommandHandler> _logger;
        private readonly GameCatalog _catalog;

        public BuyHeroShardsCommandHandler(
            IVillageRepository villageRepository,
            IHeroRepository heroRepository,
            IUnitOfWork unitOfWork,
            TimeProvider timeProvider,
            ILogger<BuyHeroShardsCommandHandler> logger,
            GameCatalog catalog)
        {
            _villageRepository = villageRepository;
            _heroRepository = heroRepository;
            _unitOfWork = unitOfWork;
            _timeProvider = timeProvider;
            _logger = logger;
            _catalog = catalog;
        }

        public async Task Handle(BuyHeroShardsCommand request, CancellationToken cancellationToken)
        {
            var now = _timeProvider.GetUtcNow().UtcDateTime;
            var settings = _catalog.Config.HeroSettings;

            var config = _catalog.FindHero(request.HeroKey)
                ?? throw new EntityNotFoundException("Hero", request.HeroKey);

            // З банерів герої приходять цілими, тож уламків для них не існує.
            // Без цієї перевірки унікального можна було б купити за золото.
            if (config.Rank != Rarity.Common)
                throw new RequirementNotMetException(
                    $"Hero '{request.HeroKey}' is {config.Rank} and cannot be bought with gold.");

            var village = await _villageRepository.GetByPlayerIdAsync(request.PlayerId, cancellationToken)
                ?? throw new InvalidOperationException($"Village not found for player {request.PlayerId}.");

            // Будівля в процесі будівництва не рахується — інакше уламки
            // можна було б купувати наперед
            var hall = village.Buildings
                .FirstOrDefault(b => b.Type == settings.BuildingKey && !b.IsUnderConstruction)
                ?? throw new RequirementNotMetException(
                    $"Summoning heroes requires a '{settings.BuildingKey}'.");

            var cost = new List<ResourceCost> { new() { Resource = "gold", Amount = config.ShardPriceGold } };

            village.ChargeCost(cost, now, request.Count);

            var progress = await _heroRepository.GetShardsAsync(request.PlayerId, request.HeroKey, cancellationToken);

            if (progress is null)
            {
                progress = new HeroShardProgress(Guid.NewGuid(), request.PlayerId, village.ServerId, request.HeroKey);
                await _heroRepository.AddShardsAsync(progress, cancellationToken);
            }

            progress.Add(request.Count);

            await _unitOfWork.SaveChangesAsync(cancellationToken);

            _logger.LogInformation(
                "Player {PlayerId} bought {Count} shards of {HeroKey} in {Building} level {Level}; {Total} shards now",
                request.PlayerId, request.Count, request.HeroKey, hall.Type, hall.Level.Value, progress.Count);
        }
    }
}
