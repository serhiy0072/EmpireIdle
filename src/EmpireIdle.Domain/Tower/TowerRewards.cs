using EmpireIdle.Domain.Services.Config;

namespace EmpireIdle.Domain.Tower
{
    /// <summary>
    /// Одноразова нагорода поверху: основа кожного поверху (віха Every 1) плюс найвища з інших віх, що ділять
    /// номер, — вища замінює нижчу, але не основу. Варіанти ступеня віхи чергуються від віхи до віхи.
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

            Add(_config.Milestones.FirstOrDefault(m => m.Every == 1));
            Add(_config.Milestones.Where(m => m.Every > 1 && floor % m.Every == 0).MaxBy(m => m.Every));

            return rewards;

            void Add(TowerMilestoneConfig? milestone)
            {
                if (milestone?.Stages.LastOrDefault(s => s.FromFloor <= floor) is { Options.Count: > 0 } stage)
                    rewards.AddRange(stage.Options[(floor / milestone.Every - 1) % stage.Options.Count]);
            }
        }
    }
}
