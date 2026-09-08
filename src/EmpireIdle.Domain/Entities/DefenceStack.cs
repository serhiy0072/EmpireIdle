namespace EmpireIdle.Domain.Entities
{
    /// <summary>
    /// Однорідна частина оборони: чиї юніти, якого типу й скільки.
    ///
    /// Власник потрібен, бо втрати адресні: свої юніти лягають у госпіталь
    /// господаря, чужі — власника підкріплення.
    /// </summary>
    /// <param name="OwnerPlayerId">
    /// null — юніти самого гарнізону. Гарнізон не знає id свого гравця
    /// (він прив'язаний до села), тож «свої» позначаються відсутністю власника.
    /// </param>
    public record DefenceStack(Guid? OwnerPlayerId, string UnitType, int Count);
}
