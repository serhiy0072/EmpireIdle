namespace EmpireIdle.Domain.ValueObjects
{
    /// <summary>
    /// Нагорода у вкладенні листа — знімок на момент відправки. На відміну від
    /// конфіга нагород, не змінюється від ребалансу: гравцю обіцяли саме це.
    /// </summary>
    /// <param name="Type">Тип нагороди, як у RewardConfig: Gems, Resource, Item…</param>
    /// <param name="Key">Ключ ресурсу чи предмета; для Gems — null.</param>
    public sealed record MailReward(string Type, string? Key, int Amount);
}
