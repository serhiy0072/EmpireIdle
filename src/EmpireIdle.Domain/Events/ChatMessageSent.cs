using EmpireIdle.Domain.Enums;

namespace EmpireIdle.Domain.Events
{
    /// <summary>
    /// Подія: у чат надіслано повідомлення. Адресатів вирішує підписник —
    /// для клану він читає членів із бази в момент доставки.
    /// </summary>
    public record ChatMessageSent(Guid MessageId, int ServerId, ChatChannel Channel, Guid? ClanId, Guid SenderId,
        Guid? RecipientId, DateTime OccurredAt) : IDomainEvent;
}
