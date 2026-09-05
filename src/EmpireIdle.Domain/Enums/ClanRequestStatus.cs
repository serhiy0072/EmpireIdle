namespace EmpireIdle.Domain.Enums
{
    /// <summary>Стан заявки або запрошення.</summary>
    public enum ClanRequestStatus
    {
        Pending = 0,
        Accepted = 1,
        Declined = 2,

        /// <summary>Знято тим, хто ініціював.</summary>
        Cancelled = 3
    }
}
