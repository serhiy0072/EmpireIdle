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
    /// <param name="Level">Власний рівень героя — за досвід із пулу.</param>
    /// <param name="EffectiveLevel">Рівень, з яким герой воює: у таборі — більший із власного й табірного (GDD §6.1).</param>
    /// <param name="CampSlot">Слот навчального табору від 0; null — поза табором.</param>
    /// <param name="SkillLevels">Рівень кожного вміння з конфіга героя, що діє зараз; 0 — ще закрите рівнем героя (GDD §6.1).</param>
    /// <param name="SkillLevelCap">До якого рівня можна підняти вміння з поточними зірками: зірки + 1.</param>
    public record HeroSummary(
        Guid Id,
        string HeroKey,
        int Tier,
        int Level,
        int EffectiveLevel,
        int? CampSlot,
        int MaxLevel,
        long ExperienceToNext,
        int StarParts,
        int? NextStarPartCost,
        int Shards,
        string State,
        Guid? StationedGarrisonId,
        bool IsLeader,
        IReadOnlyDictionary<string, int> SkillLevels,
        int SkillLevelCap);
}
