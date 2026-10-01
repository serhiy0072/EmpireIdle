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

                // Будівля під туманом нічого не виробила — і грабувати в ній нічого
                if (!village.IsProducing(building, _catalog.Buildings))
                    continue;

                var config = _catalog.Buildings[building.Type];

                var taken = building.Plunder(remaining);

                if (taken == 0)
                    continue;

                Add(loot, config.ProducesResource!, taken);
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

        /// <summary>
        /// Що можна винести з села зараз, без обмеження вантажопідйомністю, — для звіту
        /// розвідки. Ті самі правила, що й у <see cref="Plunder"/>: буфери будівель повністю,
        /// зі складу — понад захищений запас. Чиста функція: село не змінює.
        /// </summary>
        public Dictionary<string, int> Lootable(Village village, ProductionBoost boost, double locationMultiplier,
            DateTime utcNow)
        {
            var loot = new Dictionary<string, int>();

            foreach (var building in village.Buildings)
            {
                if (!village.IsProducing(building, _catalog.Buildings))
                    continue;

                var config = _catalog.Buildings[building.Type];
                var stored = building.StoredAt(config, utcNow, boost, locationMultiplier);

                if (stored > 0)
                    Add(loot, config.ProducesResource!, stored);
            }

            foreach (var resource in village.Resources)
            {
                var available = resource.Amount - _capacities.ProtectedReserveFor(village, resource.ResourceType);

                if (available > 0)
                    Add(loot, resource.ResourceType, available);
            }

            return loot;
        }

        private static void Add(Dictionary<string, int> loot, string key, int amount)
            => loot[key] = loot.GetValueOrDefault(key) + amount;
    }
}
