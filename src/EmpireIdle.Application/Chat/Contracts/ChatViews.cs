namespace EmpireIdle.Application.Chat.Contracts
{
    /// <summary>
    /// Повідомлення для конкретного читача. TranslatedText — переклад на мову
    /// читача, якщо повідомлення написане іншою і переклад є; інакше null,
    /// і клієнт показує оригінал.
    /// </summary>
    /// <param name="Channel">"Server", "Clan" або "Private".</param>
    public record ChatMessageView(
        Guid Id,
        string Channel,
        Guid SenderId,
        string SenderName,
        Guid? RecipientId,
        string Text,
        string Language,
        string? TranslatedText,
        DateTime SentAt,
        bool IsOwn);

    /// <summary>Приватна розмова в списку: з ким і останнє повідомлення.</summary>
    public record ChatConversationView(Guid PartnerId, string PartnerName, ChatMessageView LastMessage);

    /// <summary>
    /// Нове повідомлення для real-time доставки. Переклади на всі мови світу
    /// вже пораховані: хаб не знає мови кожного отримувача, клієнт бере свою.
    /// </summary>
    public record ChatMessageNotice(
        Guid Id,
        string Channel,
        Guid SenderId,
        string SenderName,
        Guid? ClanId,
        Guid? RecipientId,
        string Text,
        string Language,
        IReadOnlyDictionary<string, string> Translations,
        DateTime SentAt);
}
