using EmpireIdle.Application.Common.Security;
using EmpireIdle.Application.Heroes.Services;
using EmpireIdle.Application.Interfaces;
using EmpireIdle.Domain.Enums;
using EmpireIdle.Domain.Exceptions;
using EmpireIdle.Domain.Services;
using MediatR;
using Microsoft.Extensions.Logging;

namespace EmpireIdle.Application.Heroes.Commands
{
    /// <summary>
    /// Обмін універсальних осколків на рідкість вищу (GDD §6.1): 100 звичайних — 1 рідкісний,
    /// 300 рідкісних — 1 унікальний. Відкривається, лише коли всі герої рідкості, з якої міняють,
    /// відкриті й прокачані до кінця: інакше осколкам є куди йти і без обміну.
    /// </summary>
    public record UpgradeUniversalShardsCommand(Guid PlayerId, Rarity From, int Count)
        : IRequest, IPlayerScopedRequest, IIdempotentRequest;

    internal sealed class UpgradeUniversalShardsCommandHandler : IRequestHandler<UpgradeUniversalShardsCommand>
    {
        private readonly IHeroRepository _heroRepository;
        private readonly IInventoryRepository _inventoryRepository;
        private readonly HeroShardBank _shards;
        private readonly IUnitOfWork _unitOfWork;
        private readonly GameCatalog _catalog;
        private readonly ILogger<UpgradeUniversalShardsCommandHandler> _logger;

        public UpgradeUniversalShardsCommandHandler(
            IHeroRepository heroRepository,
            IInventoryRepository inventoryRepository,
            HeroShardBank shards,
            IUnitOfWork unitOfWork,
            GameCatalog catalog,
            ILogger<UpgradeUniversalShardsCommandHandler> logger)
        {
            _heroRepository = heroRepository;
            _inventoryRepository = inventoryRepository;
            _shards = shards;
            _unitOfWork = unitOfWork;
            _catalog = catalog;
            _logger = logger;
        }

        public async Task Handle(UpgradeUniversalShardsCommand request, CancellationToken cancellationToken)
        {
            var settings = _catalog.Config.HeroSettings;

            // Обміну з найвищої рідкості немає: валідатор не пускає такий ключ у конфіг
            if (!settings.UniversalShardUpgrade.TryGetValue(request.From.ToString(), out var rate))
                throw new RequirementNotMetException($"Universal shards of {request.From} cannot be upgraded.");

            var to = request.From + 1;

            var owned = (await _heroRepository.GetByPlayerReadOnlyAsync(request.PlayerId, cancellationToken))
                .ToDictionary(h => h.HeroKey);

            var notMaxed = _catalog.Config.Heroes
                .Where(h => h.Rank == request.From)
                .Any(h => !owned.TryGetValue(h.Key, out var hero) || hero.StarParts < settings.MaxStarParts);

            if (notMaxed)
                throw new RequirementNotMetException(RefusalReasons.ShardUpgradeLocked,
                    $"Every {request.From} hero must be unlocked with every star first.", request.From.ToString());

            var fromKey = _shards.UniversalItemKey(request.From);
            var cost = checked(rate * request.Count);

            var stack = await _inventoryRepository.GetItemAsync(request.PlayerId, fromKey, cancellationToken)
                ?? throw new NotEnoughResourcesException(fromKey, cost, 0);

            stack.Consume(cost);
            await _shards.AddUniversalAsync(request.PlayerId, to, request.Count, cancellationToken);

            await _unitOfWork.SaveChangesAsync(cancellationToken);

            _logger.LogInformation("Player {PlayerId} upgraded {Cost} {From} universal shards into {Count} {To}",
                request.PlayerId, cost, request.From, request.Count, to);
        }
    }
}
