using EmpireIdle.Domain.Entities;
using EmpireIdle.Domain.Services.Config;

namespace EmpireIdle.Domain.Services
{
    /// <summary>
    /// Скільки що вміщає. Обчислення від конфіга й рівнів будівель —
    /// стан села не змінюють, тож в агрегаті їм не місце.
    /// </summary>
    public sealed class VillageCapacities
    {
        private readonly GameCatalog _catalog;

        public VillageCapacities(GameCatalog catalog) => _catalog = catalog;

        /// <summary>
        /// Місткість сховища для ресурсу. Золото в банку, решта на складі:
        /// два різні сховища, два різні рівні. Будівля під будівництвом
        /// місткості не дає.
        /// </summary>
        public int StorageCapFor(Village village, string resourceKey)
        {
            var storage = StorageFor(village, resourceKey, out var config);

            if (config is null)
                return int.MaxValue;

            return storage is null ? 0 : config.BaseStorage * storage.Level.Value;
        }

        /// <summary>
        /// Недоторканий запас — абсолютне число від рівня сховища.
        /// Не частка від місткості: інакше качання складу піднімало б
        /// і захист, і здобич, і гравець не мав би важеля.
        /// </summary>
        public int ProtectedReserveFor(Village village, string resourceKey)
        {
            var storage = StorageFor(village, resourceKey, out var config);

            return storage is null || config is null ? 0 : config.ProtectedStorage * storage.Level.Value;
        }

        /// <summary>Скільки чужих юнітів вміщає посольство.</summary>
        public int ReinforcementSlots(Village village)
            => SumPerLevel(village, c => c.ReinforcementSlotsPerLevel);

        private int SumPerLevel(Village village, Func<BuildingConfig, int> perLevel)
            => village.Buildings
                .Where(b => !b.IsUnderConstruction)
                .Sum(b => _catalog.Buildings.TryGetValue(b.Type, out var config) ? perLevel(config) * b.Level.Value : 0);

        /// <summary>Готове сховище для ресурсу; config null — ресурс ніде не зберігається.</summary>
        private Building? StorageFor(Village village, string resourceKey, out BuildingConfig? config)
        {
            config = _catalog.Buildings.Values
                .FirstOrDefault(c => c.StoresResources?.Contains(resourceKey) == true);

            if (config is null)
                return null;

            var key = config.Key;

            return village.Buildings.FirstOrDefault(b => b.Type == key && !b.IsUnderConstruction);
        }
    }
}
