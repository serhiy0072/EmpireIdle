using EmpireIdle.Domain.Services.Config;

namespace EmpireIdle.Domain.Tower
{
    /// <summary>
    /// Одноразова нагорода поверху (дизайн §8.6): золото кожного поверху плюс найвища віха, що ділить
    /// його номер, — вища замінює нижчу. Варіанти ступеня віхи чергуються від віхи до віхи.
    /// </summary>
    public sealed class TowerRewards
    {
        private readonly TowerConfig _config;

        public TowerRewards(TowerConfig config)
        {
            _config = config;
        }

        public IReadOnlyList<RewardConfig> For(int floor)
        {
            var rewards = new List<RewardConfig>();
            var gold = _config.GoldBase + _config.GoldPerFloor * floor;

            if (gold > 0)
                rewards.Add(new RewardConfig { Type = "Resource", Key = _config.GoldResource, Amount = gold });

            var milestone = _config.Milestones
                .Where(m => m.Every > 0 && floor % m.Every == 0)
                .MaxBy(m => m.Every);

            if (milestone?.Stages.LastOrDefault(s => s.FromFloor <= floor) is { Options.Count: > 0 } stage)
                rewards.AddRange(stage.Options[(floor / milestone.Every - 1) % stage.Options.Count]);

            return rewards;
        }
    }
}
