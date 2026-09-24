namespace EmpireIdle.Domain.Entities
{
    /// <summary>
    /// Кеш перекладу повідомлення на одну мову. Переклад платний і повільний,
    /// тож кожна пара «повідомлення + мова» перекладається рівно один раз,
    /// скільки б гравців її не читало.
    /// </summary>
    public class ChatTranslation
    {
        public Guid MessageId { get; private set; }

        public string Language { get; private set; } = null!;

        public string Text { get; private set; } = null!;

        public ChatTranslation(Guid messageId, string language, string text)
        {
            MessageId = messageId;
            Language = language;
            Text = text;
        }

        protected ChatTranslation() { } // Для EF Core
    }
}
