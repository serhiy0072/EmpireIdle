using EmpireIdle.Domain.Services;

namespace EmpireIdle.Domain.Services
{
    /// <summary>Будує склад загону монстра за його типом і рівнем.</summary>
    public class MonsterArmyBuilder
    {
        private readonly List<MonsterConfig> _configs;

        public MonsterArmyBuilder(List<MonsterConfig> configs)
        {
            _configs = configs;
        }

        /// <summary>
        /// Склад монстра: кількість юнітів росте геометрично з рівнем.
        /// </summary>
        public Dictionary<string, int> BuildArmy(string monsterType, int level)
        {
            var config = GetConfig(monsterType);
            var levelsAbove = Math.Max(0, level - config.MinLevel);
            var multiplier = Math.Pow(config.UnitGrowth, levelsAbove);

            return config.Units.ToDictionary(
                u => u.UnitType,
                u => Math.Max(1, (int)Math.Round(u.Count * multiplier)));
        }

        /// <summary>Нагорода за перемогу: росте з рівнем монстра.</summary>
        public List<ResourceCost> BuildRewards(string monsterType, int level)
        {
            var config = GetConfig(monsterType);
            var levelsAbove = Math.Max(0, level - config.MinLevel);
            var multiplier = Math.Pow(config.RewardGrowth, levelsAbove);

            return config.Rewards
                .Select(r => new ResourceCost
                {
                    Resource = r.Resource,
                    Amount = Math.Max(1, (int)Math.Round(r.Amount * multiplier))
                })
                .ToList();
        }

        private MonsterConfig GetConfig(string monsterType)
            => _configs.FirstOrDefault(m => m.Key == monsterType)
                ?? throw new InvalidOperationException($"Unknown monster type '{monsterType}'.");
    }
}