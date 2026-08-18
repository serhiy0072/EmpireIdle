namespace EmpireIdle.Domain.Services
{
    /// <summary>Ярус нагороди за рангом внеску в серверний квест.</summary>
    public class RewardTierConfig
    {
        /// <summary>Верхня межа рангу; null — «всі інші».</summary>
        public int? MaxRank { get; set; }

        public List<RewardConfig> Rewards { get; set; } = new();
    }
}
