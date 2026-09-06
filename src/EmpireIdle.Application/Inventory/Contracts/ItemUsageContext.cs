using EmpireIdle.Domain.Services.Config;

namespace EmpireIdle.Application.Inventory.Contracts
{
    /// <summary>
    /// Контекст застосування предмета.
    /// TargetX/TargetY — для предметів, що діють на клітину карти, а не на сутність.
    /// </summary>
    public record ItemUsageContext(Guid PlayerId, ItemConfig Config, int Count, Guid? TargetId, DateTime UtcNow, int? TargetX = null, int? TargetY = null);
}
