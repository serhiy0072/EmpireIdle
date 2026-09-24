namespace EmpireIdle.Application.Mail.Contracts
{
    /// <summary>Скринька гравця: особисті листи, оголошення світу й лічильник непрочитаних.</summary>
    public record MailboxView(IReadOnlyList<MailLetterView> Letters, IReadOnlyList<AnnouncementView> Announcements, int Unread);

    /// <summary>
    /// Особистий лист. Деталі — лише свого виду, і вони актуальні на момент
    /// читання: лист посилається, а не копіює (GDD §7.4).
    /// </summary>
    /// <param name="Kind">"ClanInvite".</param>
    public record MailLetterView(Guid Id, string Kind, DateTime CreatedAt, bool IsRead, ClanInviteLetterView? ClanInvite);

    /// <summary>
    /// Запрошення в клан за його теперішнім станом. CanRespond — лише поки
    /// воно чекає й не протерміноване: після дії лист лишається без кнопок.
    /// </summary>
    /// <param name="State">"Pending", "Accepted", "Declined", "Cancelled", "Expired" або "Gone" (клан розпущено).</param>
    public record ClanInviteLetterView(Guid RequestId, Guid ClanId, string ClanName, string ClanTag, string State,
        DateTime ExpiresAt, bool CanRespond);

    /// <param name="Kind">"News", "Event" або "Maintenance".</param>
    public record AnnouncementView(Guid Id, string Kind, string Title, string Body, DateTime PublishedAt, bool IsRead);
}
