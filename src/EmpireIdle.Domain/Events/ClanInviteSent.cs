namespace EmpireIdle.Domain.Events
{
    /// <summary>
    /// Подія: гравцеві надіслали запрошення в клан.
    ///
    /// Назви клану тут немає навмисно: подія переживає зміну опису й тега,
    /// а підписник підтягне актуальну картку сам.
    /// </summary>
    public record ClanInviteSent(Guid RequestId, Guid ClanId, Guid PlayerId, DateTime ExpiresAt,
        DateTime OccurredAt) : IDomainEvent;
}
