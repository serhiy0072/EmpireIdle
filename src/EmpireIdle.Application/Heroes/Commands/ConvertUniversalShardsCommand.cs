using EmpireIdle.Application.Common.Security;
using EmpireIdle.Application.Heroes.Services;
using EmpireIdle.Application.Interfaces;
using EmpireIdle.Domain.Exceptions;
using EmpireIdle.Domain.Services;
using MediatR;
using Microsoft.Extensions.Logging;

namespace EmpireIdle.Application.Heroes.Commands
{
    /// <summary>
    /// Перетворює універсальні осколки в осколки героя 1:1 (GDD §6.1). Лише для вже відкритого героя
    /// і лише своєї рідкості: універсальні — для прокачки зірок, а не для призову.
    /// </summary>
    public record ConvertUniversalShardsCommand(Guid PlayerId, string HeroKey, int Count)
        : IRequest, IPlayerScopedRequest, IIdempotentRequest;

    internal sealed class ConvertUniversalShardsCommandHandler : IRequestHandler<ConvertUniversalShardsCommand>
    {
        private readonly IHeroRepository _heroRepository;
        private readonly IInventoryRepository _inventoryRepository;
        private readonly HeroShardBank _shards;
        private readonly IUnitOfWork _unitOfWork;
        private readonly GameCatalog _catalog;
        private readonly ILogger<ConvertUniversalShardsCommandHandler> _logger;

        public ConvertUniversalShardsCommandHandler(
            IHeroRepository heroRepository,
            IInventoryRepository inventoryRepository,
            HeroShardBank shards,
            IUnitOfWork unitOfWork,
            GameCatalog catalog,
            ILogger<ConvertUniversalShardsCommandHandler> logger)
        {
            _heroRepository = heroRepository;
            _inventoryRepository = inventoryRepository;
            _shards = shards;
            _unitOfWork = unitOfWork;
            _catalog = catalog;
            _logger = logger;
        }

        public async Task Handle(ConvertUniversalShardsCommand request, CancellationToken cancellationToken)
        {
            var config = _catalog.FindHero(request.HeroKey)
                ?? throw new EntityNotFoundException("Hero", request.HeroKey);

            var hero = await _heroRepository.GetByKeyAsync(request.PlayerId, request.HeroKey, cancellationToken)
                ?? throw new RequirementNotMetException(RefusalReasons.HeroNotOwned,
                    $"Universal shards go only into an unlocked hero, and '{request.HeroKey}' is not.", config.DisplayName);

            // Прокачаному героєві осколки нічого не дадуть — і повернулись би універсальними ж
            if (hero.StarParts >= _catalog.Config.HeroSettings.MaxStarParts)
                throw new RequirementNotMetException(RefusalReasons.HeroMaxStars,
                    $"Hero '{request.HeroKey}' already has every star.", _catalog.Config.HeroSettings.MaxStarParts);

            var itemKey = _shards.UniversalItemKey(config.Rank);
            var stack = await _inventoryRepository.GetItemAsync(request.PlayerId, itemKey, cancellationToken)
                ?? throw new NotEnoughResourcesException(itemKey, request.Count, 0);

            stack.Consume(request.Count);

            var progress = await _shards.GetOrCreateShardsAsync(request.PlayerId, request.HeroKey, cancellationToken);
            progress.Add(request.Count);

            await _unitOfWork.SaveChangesAsync(cancellationToken);

            _logger.LogInformation("Player {PlayerId} turned {Count} {Item} into shards of {HeroKey}",
                request.PlayerId, request.Count, itemKey, request.HeroKey);
        }
    }
}
