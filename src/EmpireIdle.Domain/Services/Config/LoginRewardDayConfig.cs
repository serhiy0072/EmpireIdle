namespace EmpireIdle.Domain.Services.Config
{
    /// <summary>Нагорода одного дня серії входів.</summary>
    public class LoginRewardDayConfig
    {
        public List<RewardConfig> Rewards { get; set; } = new();
    }
}
