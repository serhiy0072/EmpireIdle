using EmpireIdle.Domain.Services;

namespace EmpireIdle.Application.Rewards
{
    /// <summary>Контекст видачі нагороди.</summary>
    /// <param name="PlayerId">Кому видаємо.</param>
    /// <param name="Reward">Опис нагороди з конфіга.</param>
    /// <param name="Reference">Джерело — ключ квесту, віхи, лутбокса. Іде в лог і транзакцію гаманця.</param>
    public record RewardContext(Guid PlayerId, RewardConfig Reward, string Reference, DateTime UtcNow);

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
