namespace EmpireIdle.Domain.Enums
{

    /// <summary>Тип цілі походу.</summary>
    public enum MarchTargetType
    {
        Monster = 1,
        Village = 2,
        ClanStructure = 3,

        /// <summary>Табір чужої армії (§2.5); TargetId — id маршу-табору.</summary>
        Camp = 4
    }
}
