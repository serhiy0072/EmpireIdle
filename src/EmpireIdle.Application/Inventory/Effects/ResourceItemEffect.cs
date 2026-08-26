using EmpireIdle.Application.Interfaces;
using EmpireIdle.Domain.Services;

namespace EmpireIdle.Application.Inventory.Effects
{
    /// <summary>Ящик ресурсів: додає ресурси в село.</summary>
    public class ResourceItemEffect : IItemEffect
    {
        public string ItemType => "resources";

        private readonly IVillageRepository _villageRepository;
        private readonly TimeProvider _timeProvider;

        public ResourceItemEffect(IVillageRepository villageRepository, TimeProvider timeProvider)
        {
            _villageRepository = villageRepository;
            _timeProvider = timeProvider;
        }

        public async Task ApplyAsync(ItemUsageContext context, CancellationToken cancellationToken)
        {
            var now = _timeProvider.GetUtcNow().UtcDateTime;

            if (context.Config.Resources.Count == 0)
                throw new InvalidOperationException($"Item '{context.Config.Key}' has no resources configured.");

            var village = await _villageRepository.GetByPlayerIdAsync(context.PlayerId, cancellationToken)
                ?? throw new InvalidOperationException($"Village not found for player {context.PlayerId}.");

            var rewards = context.Config.Resources
                .Select(r => new ResourceCost { Resource = r.Resource, Amount = r.Amount * context.Count })
                .ToList();

            village.GrantResources(rewards, now);
        }
    }
}
