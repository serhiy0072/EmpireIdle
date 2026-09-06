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
            foreach (var terrain in _config.Terrains.Where(t => t.Weight > 0).OrderBy(t => t.Type))
            {
                cumulative += terrain.Weight;
                _distribution.Add((terrain, cumulative));
            }
            _totalWeight = cumulative;

            if (_totalWeight <= 0)
                throw new InvalidOperationException("Map config must contain at least one terrain with positive weight.");
        }

        /// <summary>Повний опис місцевості клітини (тип + прохідність + вартість руху).</summary>
        public TerrainConfig GetTerrain(int serverId, int x, int y)
        {
            var roll = Hash(serverId, x, y) % _totalWeight;

            foreach (var (terrain, cumulativeWeight) in _distribution)
                if (roll < cumulativeWeight)
                    return terrain;

            return _distribution[^1].Terrain; // недосяжно: roll завжди < _totalWeight
        }

        /// <summary>Ключ типу місцевості клітини.</summary>
        public string GetTerrainType(int serverId, int x, int y)
            => GetTerrain(serverId, x, y).Type;

        /// <summary>Чи може армія проходити через клітину.</summary>
        public bool IsPassable(int serverId, int x, int y)
            => GetTerrain(serverId, x, y).Passable;

        /// <summary>Чи можна розмістити село або монстра на клітині.</summary>
        public bool IsHabitable(int serverId, int x, int y)
            => GetTerrain(serverId, x, y).Habitable;

        /// <summary>Множник часу проходу через клітину.</summary>
        public double GetMoveCost(int serverId, int x, int y)
            => GetTerrain(serverId, x, y).MoveCost;

        /// <summary>Чи лежить клітина в межах карти.</summary>
        public bool IsInBounds(int x, int y)
            => x >= 0 && x < _config.Width && y >= 0 && y < _config.Height;

        /// <summary>
        /// Змішує координати й сід у псевдовипадкове невід'ємне число.
        /// Прості множники — щоб сусідні клітини не давали схожих результатів.
        /// </summary>
        private int Hash(int serverId, int x, int y)
        {
            unchecked
            {
                var h = _config.TerrainSeed;
                h = h * 31 + serverId;
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
