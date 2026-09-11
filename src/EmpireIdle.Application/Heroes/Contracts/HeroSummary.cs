namespace EmpireIdle.Application.Heroes.Contracts
{
    /// <summary>
    /// Стан одного героя разом зі стелею рівня.
    ///
    /// MaxLevel рахує сервер: він залежить від ратуші й тіру, і дублювання
    /// формули на клієнті розійшлося б із бекендом на першій зміні балансу.
    ///
    /// Довідникових полів із heroes.json тут немає навмисно — вони однакові
    /// для всіх гравців і возити їх у кожній відповіді немає сенсу.
    /// </summary>
    public record HeroSummary(
        Guid Id,
        string HeroKey,
        int Tier,
        int Level,
        int MaxLevel,
        int Constellation,
        string State,
        DateTime? HealedAt);
}
