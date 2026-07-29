using System;
using System.Collections.Generic;
using System.Text;

namespace EmpireIdle.Domain.Services
{
    /// <summary>
    /// Детермінований генератор типу місцевості для клітини карти.
    /// Терейн НЕ зберігається в БД: той самий (serverId, x, y) завжди дає той самий тип,
    /// тому карта «існує» без жодного рядка в базі.
    /// </summary>
    public class TerrainGenerator
    {
        private readonly MapConfig _config;
        private readonly List<(string Type, int ComulativeWeight)> _distribution;
        private readonly int _totalWeight;

        public TerrainGenerator(MapConfig config)
        {
            _config = config;
            _distribution = new List<(string, int)>();

            // Будуємо кумулятивний розподіл один раз: [(plain,40),(forest,65),(mountain,85),(water,100)]
            int cumulative = 0;
            foreach (var (type, weight) in _config.TerrainWeights.OrderBy(w => w.Key))
            {
                cumulative += weight;
                _distribution.Add((type, cumulative));
            }
            _totalWeight = cumulative;

            if (cumulative <= 0)
                throw new InvalidOperationException("Map TerrainWeights must contain at least one positive weight.");
        }

        /// <summary>Тип місцевості клітини. Детермінований для однакових вхідних даних.</summary>
        public String GetTerrain(int serverId, int x, int y)
        {
            var roll = Hash(serverId, x, y) % _totalWeight;
            foreach(var (type, cumulativeWeight) in _distribution)
            {
                if (roll < cumulativeWeight)
                    return type;
            }
            return _distribution[^1].Type; //недосяжно, але компілятор має бути щасливий
        }

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
                return Math.Abs(h);
            }
        }
    }
}
