using EmpireIdle.Application.Interfaces;
using EmpireIdle.Domain.Services;

namespace EmpireIdle.Application.Inventory.Effects
{
    /// <summary>Ящик ресурсів: додає ресурси в село в межах капу складу.</summary>
    public class ResourceItemEffect : IItemEffect
    {
        public string ItemType => "resources";

        private readonly IVillageRepository _villageRepository;
        private readonly GameCatalog _catalog;

        public ResourceItemEffect(IVillageRepository villageRepository, GameCatalog catalog)
        {
            _villageRepository = villageRepository;
            _catalog = catalog;
        }

        public async Task ApplyAsync(ItemUsageContext context, CancellationToken cancellationToken)
        {
            if (context.Config.Resources.Count == 0)
                throw new InvalidOperationException($"Item '{context.Config.Key}' has no resources configured.");

            var village = await _villageRepository.GetByPlayerIdAsync(context.PlayerId, cancellationToken)
                ?? throw new InvalidOperationException($"Village not found for player {context.PlayerId}.");

            // Той самий шлях, що й нагороди квестів: надлишок понад кап згорає
            foreach (var line in context.Config.Resources)
                village.GrantResource(line.Resource, line.Amount * context.Count, _catalog.Buildings, context.UtcNow);
        }
    }
}
