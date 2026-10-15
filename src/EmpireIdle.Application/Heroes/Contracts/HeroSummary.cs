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
    /// <param name="IsDefender">Герой зараз боронить село — найсильніший свого класу (рішення 15.10.2026), навіть у марші.</param>
    /// <param name="SkillLevels">Рівень кожного вміння з конфіга героя, що діє зараз; 0 — ще закрите рівнем героя (GDD §6.1).</param>
    /// <param name="UnitType">Тип юнітів, яких веде герой своєї ролі (GDD §6.1); null — роль без юнітів у конфігу.</param>
    /// <param name="ConvoyCapacity">Скільки юнітів герой веде в марш: конвої за рівнем × розмір конвою.</param>
    /// <param name="Convoys">Скільки конвоїв герой веде на своєму рівні (GDD §6.1).</param>
    /// <param name="Power">Сила героя разом із вдягненим спорядженням і зброєю — та, що на екрані героя.</param>
    /// <param name="WeaponLevel">Рівень унікальної зброї (GDD §6.4): 0 — не відкрита, далі +1…+5.</param>
    /// <param name="WeaponShards">Шматки зброї, ще не вкладені в рівень.</param>
    /// <param name="NextWeaponCost">Шматків на наступний рівень; null — зброя на стелі.</param>
    /// <param name="SkillLevelCap">До якого рівня можна підняти вміння з поточними зірками: зірки + 1.</param>
    /// <param name="Stats">Стати героя зараз — разом зі зброєю, зірками й спорядженням.</param>
    /// <param name="NextLevelStats">Ті самі стати після підняття рівня; null — рівень на стелі або табір тримає вищий.</param>
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
        bool IsDefender,
        IReadOnlyDictionary<string, int> SkillLevels,
        int SkillLevelCap,
        string? UnitType,
        int ConvoyCapacity,
        int Convoys,
        double Power,
        int WeaponLevel,
        int WeaponShards,
        int? NextWeaponCost,
        IReadOnlyDictionary<string, double> Stats,
        IReadOnlyDictionary<string, double>? NextLevelStats);
}
