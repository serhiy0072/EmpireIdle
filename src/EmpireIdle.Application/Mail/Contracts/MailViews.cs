namespace EmpireIdle.Application.Mail.Contracts
{
    /// <summary>Скринька гравця: особисті листи, оголошення світу й лічильник непрочитаних.</summary>
    public record MailboxView(IReadOnlyList<MailLetterView> Letters, IReadOnlyList<AnnouncementView> Announcements, int Unread);

    /// <summary>
    /// Особистий лист. Деталі — лише свого виду, і вони актуальні на момент
    /// читання: лист посилається, а не копіює (GDD §7.4).
    /// </summary>
    /// <param name="Kind">"ClanInvite", "CityFall", "DailyReward", "WeeklyReward" або "MonthlyReward".</param>
    /// <param name="Rewards">Вкладення; порожнє — лист без нагороди.</param>
    /// <param name="Sequence">Порядковий номер у серії — день для щоденної нагороди.</param>
    /// <param name="CanClaim">Вкладення ще не забране й не згоріло.</param>
    public record MailLetterView(Guid Id, string Kind, DateTime CreatedAt, DateTime ExpiresAt, bool IsRead,
        ClanInviteLetterView? ClanInvite, CityFallLetterView? CityFall, StructureFallLetterView? StructureFall,
        IReadOnlyList<MailRewardView> Rewards, int? Sequence, DateTime? ClaimedAt, bool CanClaim);

    /// <param name="Type">Gems, Resource, Item… — як у конфігу нагород.</param>
    public record MailRewardView(string Type, string? Key, int Amount);

    /// <summary>
    /// Запрошення в клан за його теперішнім станом. CanRespond — лише поки
    /// воно чекає й не протерміноване: після дії лист лишається без кнопок.
    /// </summary>
    /// <param name="State">"Pending", "Accepted", "Declined", "Cancelled", "Expired" або "Gone" (клан розпущено).</param>
    public record ClanInviteLetterView(Guid RequestId, Guid ClanId, string ClanName, string ClanTag, string State,
        DateTime ExpiresAt, bool CanRespond);

    /// <summary>Падіння міста: хто виселив, звідки й куди, до коли щит.</summary>
    public record CityFallLetterView(string AttackerName, int FromX, int FromY, int ToX, int ToY, DateTime ShieldUntil);

    /// <summary>Споруду клану зруйновано: де стояла й хто зруйнував (назва села — знімок на момент бою).</summary>
    public record StructureFallLetterView(string AttackerName, int X, int Y, DateTime OccurredAt);

    /// <param name="Kind">"News", "Event" або "Maintenance".</param>
    public record AnnouncementView(Guid Id, string Kind, string Title, string Body, DateTime PublishedAt, bool IsRead);
}
