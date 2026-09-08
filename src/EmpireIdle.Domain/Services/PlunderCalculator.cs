using EmpireIdle.Domain.Entities;
using EmpireIdle.Domain.ValueObjects;

namespace EmpireIdle.Domain.Services
{
    /// <summary>
    /// Рахує здобич із села. Що взяти — вирішує сервіс, забирає агрегат:
    /// правило «скільки можна» залежить від конфіга, а «як списати» —
    /// від інваріантів села.
    /// </summary>
    public sealed class PlunderCalculator
    {
        private readonly GameCatalog _catalog;
        private readonly VillageCapacities _capacities;

        public PlunderCalculator(GameCatalog catalog, VillageCapacities capacities)
        {
            _catalog = catalog;
            _capacities = capacities;
        }

        /// <summary>
        /// Буфери будівель ідуть повністю — невибраний виробіток захисту
        /// не має. Зі складу й банку береться лише понад захищений запас,
        /// і не більше, ніж армія здатна винести.
        /// </summary>
        public Dictionary<string, int> Plunder(Village village, int carryCapacity,
            ProductionBoost boost, double locationMultiplier, DateTime utcNow)
        {
            var loot = new Dictionary<string, int>();
            var remaining = Math.Max(0, carryCapacity);

            if (remaining == 0)
                return loot;

            // Буфер живе як функція часу — щоб забрати частину,
            // його спершу треба осадити
            village.MaterializeProduction(_catalog.Buildings, utcNow, boost, locationMultiplier);

            foreach (var building in village.Buildings)
            {
                if (remaining == 0)
                    break;

                if (!_catalog.Buildings.TryGetValue(building.Type, out var config) || config.ProducesResource is null)
                    continue;

                var taken = building.Plunder(remaining);

                if (taken == 0)
                    continue;

                Add(loot, config.ProducesResource, taken);
                remaining -= taken;
            }

            foreach (var resource in village.Resources)
            {
                if (remaining == 0)
                    break;

                var available = resource.Amount - _capacities.ProtectedReserveFor(village, resource.ResourceType);

                if (available <= 0)
                    continue;

                var taken = Math.Min(available, remaining);

                village.TakeFromStore(resource.ResourceType, taken, utcNow);

                Add(loot, resource.ResourceType, taken);
                remaining -= taken;
            }

            return loot;
        }

        private static void Add(Dictionary<string, int> loot, string key, int amount)
            => loot[key] = loot.GetValueOrDefault(key) + amount;
    }
}
