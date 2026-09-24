namespace EmpireIdle.Application.LoginRewards.Contracts
{
    /// <summary>
    /// Підсумок входу: день серії (null — сьогодні вже заходив) і чи прийшли
    /// нагороди тижня й місяця. Letters — скільки листів покладено в скриньку.
    /// </summary>
    public record CheckInView(int? DailyDay, bool Weekly, bool Monthly, int Letters);
}
