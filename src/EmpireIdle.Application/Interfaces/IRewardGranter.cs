using EmpireIdle.Application.Rewards.Contracts;

namespace EmpireIdle.Application.Interfaces
{
    /// <summary>
    /// Видача одного типу нагороди. Джерело байдуже —
    /// квест, віха, івент, level up і лутбокс користуються тими самими реалізаціями.
    /// </summary>
    public interface IRewardGranter
    {
        /// <summary>Тип нагороди з конфіга, який обробляє ця реалізація.</summary>
        string RewardType { get; }

        Task GrantAsync(RewardContext context, CancellationToken cancellationToken);
    }
}
