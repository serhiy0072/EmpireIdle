using EmpireIdle.Domain.Services.Config;

namespace EmpireIdle.Domain.Services
{
    /// <summary>
    /// Детермінований генератор місцевості для клітини карти.
    /// Терейн НЕ зберігається в БД: той самий (serverId, x, y) завжди дає той самий тип,
    /// тому карта «існує» без жодного рядка в базі.
    /// </summary>
    public class TerrainGenerator
    {
        private readonly MapConfig _config;
        private readonly List<(TerrainConfig Terrain, int CumulativeWeight)> _distribution;
        private readonly int _totalWeight;

        public TerrainGenerator(MapConfig config)
        {
            _config = config;

            // Кумулятивний розподіл будуємо один раз: [(plain,35),(forest,57),(mountain,75)…]
            // Сортування за Type — щоб порядок не залежав від порядку в JSON (детермінованість).
            var cumulative = 0;
            _distribution = new List<(TerrainConfig, int)>();
            foreach (var terrain in _config.Terrains.Where(t => t.Weight > 0).OrderBy(t => t.Type, StringComparer.Ordinal))
            {
                cumulative += terrain.Weight;
                _distribution.Add((terrain, cumulative));
            }
            _totalWeight = cumulative;

            if (_totalWeight <= 0)
                throw new InvalidOperationException("Map config must contain at least one terrain with positive weight.");
        }

        /// <summary>
        /// Повний опис місцевості клітини (тип + прохідність + вартість руху). Місцевість лежить плямами:
        /// карта ділиться на ділянки TerrainPatchSize×TerrainPatchSize з центром у випадковій клітині,
        /// і клітина бере тип найближчого центру — ліси, озера й гори замість шахівниці. Тип центру —
        /// зважений кидок, тож частки типів на великій карті ті самі, що ваги в конфізі.
        /// </summary>
        public TerrainConfig GetTerrain(int serverId, int x, int y)
        {
            var size = _config.TerrainPatchSize;
            var patchX = FloorDiv(x, size);
            var patchY = FloorDiv(y, size);
            var nearest = (X: patchX, Y: patchY);
            var best = long.MaxValue;

            // Найближчий центр — серед своєї ділянки й восьми сусідніх: далі центрів ближчих не буває
            for (var dx = -1; dx <= 1; dx++)
                for (var dy = -1; dy <= 1; dy++)
                {
                    var (cx, cy) = (patchX + dx, patchY + dy);
                    var siteX = cx * size + Hash(serverId, cx, cy, salt: 1) % size;
                    var siteY = cy * size + Hash(serverId, cx, cy, salt: 2) % size;
                    var distance = (long)(siteX - x) * (siteX - x) + (long)(siteY - y) * (siteY - y);

                    if (distance < best)
                        (best, nearest) = (distance, (cx, cy));
                }

            var roll = Hash(serverId, nearest.X, nearest.Y, salt: 0) % _totalWeight;

            foreach (var (terrain, cumulativeWeight) in _distribution)
                if (roll < cumulativeWeight)
                    return terrain;

            return _distribution[^1].Terrain; // недосяжно: roll завжди < _totalWeight
        }

        private static int FloorDiv(int value, int divisor) => (int)Math.Floor(value / (double)divisor);

        /// <summary>Ключ типу місцевості клітини.</summary>
        public string GetTerrainType(int serverId, int x, int y)
            => GetTerrain(serverId, x, y).Type;

        /// <summary>Чи можна розмістити село або монстра на клітині.</summary>
        public bool IsHabitable(int serverId, int x, int y)
            => GetTerrain(serverId, x, y).Habitable;

        /// <summary>Чи лежить клітина в межах карти.</summary>
        public bool IsInBounds(int x, int y)
            => x >= 0 && x < _config.Width && y >= 0 && y < _config.Height;

        /// <summary>
        /// Змішує координати й сід у псевдовипадкове невід'ємне число.
        /// Прості множники — щоб сусідні клітини не давали схожих результатів; salt розводить незалежні
        /// кидки для тієї самої точки (тип ділянки й зсув її центру).
        /// </summary>
        private int Hash(int serverId, int x, int y, int salt)
        {
            unchecked
            {
                var h = _config.TerrainSeed;
                h = h * 31 + serverId;
                h = h * 1000003 + salt;
                h = h * 486187739 + x;
                h = h * (int)2654435761 + y;
                h ^= h >> 13;
                h *= 1274126177;
                h ^= h >> 16;
                return h & 0x7FFFFFFF;
            }
        }
    }
}
