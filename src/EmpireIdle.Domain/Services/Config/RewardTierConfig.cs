namespace EmpireIdle.Domain.Services.Config
{
    /// <summary>Ярус бонусу за рангом внеску в серверний квест — понад нагороду для всіх.</summary>
    public class RewardTierConfig
    {
        /// <summary>Верхня межа рангу; null — усі інші, хто вніс.</summary>
        public int? MaxRank { get; set; }

        public List<RewardConfig> Rewards { get; set; } = new();
    }
}
