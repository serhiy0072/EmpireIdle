namespace EmpireIdle.Domain.Enums
{
    /// <summary>
    /// Тип транзакції в гаманці гравця. Зберігається числом — значення явні,
    /// щоб перестановка членів не переписала мовчки історію журналу.
    /// </summary>
    public enum TransactionType
    {
        /// <summary>Gems, куплені за гроші.</summary>
        GemPurchase = 0,

        GemSpend = 1,
        SealEarned = 2,
        SealSpend = 3,

        /// <summary>Gems із нагороди (квест, лист, подія) — не купівля.</summary>
        GemReward = 4
    }
}