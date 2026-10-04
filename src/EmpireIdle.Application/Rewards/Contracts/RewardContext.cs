using EmpireIdle.Domain.Services.Config;

namespace EmpireIdle.Application.Rewards.Contracts
{
    /// <summary>Контекст видачі нагороди.</summary>
    /// <param name="PlayerId">Кому видаємо.</param>
    /// <param name="Reward">Опис нагороди з конфіга.</param>
    /// <param name="Reference">Джерело — ключ квесту, віхи, лутбокса. Іде в лог і транзакцію гаманця.</param>
    public record RewardContext(Guid PlayerId, RewardConfig Reward, string Reference, DateTime UtcNow);
}
