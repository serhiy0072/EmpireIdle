using EmpireIdle.Domain.Enums;
using EmpireIdle.Domain.Events;

namespace EmpireIdle.Domain.Entities
{
    /// <summary>
    /// Повідомлення чату (GDD §7.3). Незмінне після надсилання: редагування
    /// й видалення в першій версії немає, тож і токена паралелізму теж.
    ///
    /// Мова — мова інтерфейсу відправника на момент надсилання: саме з неї
    /// перекладач переводить для тих, у кого інша.
    /// </summary>
    public class ChatMessage : Entity
    {
        public int ServerId { get; private set; }

        public ChatChannel Channel { get; private set; }

        /// <summary>Клан — лише для кланового каналу.</summary>
        public Guid? ClanId { get; private set; }

        public Guid SenderId { get; private set; }

        /// <summary>Адресат — лише для приватного каналу.</summary>
        public Guid? RecipientId { get; private set; }

        public string Text { get; private set; } = null!;

        public string Language { get; private set; } = null!;

        public DateTime SentAt { get; private set; }

        /// <param name="text">Уже обрізаний від пробілів і перевірений на довжину викликачем — межа живе в конфігу.</param>
        public ChatMessage(Guid id, int serverId, ChatChannel channel, Guid? clanId, Guid senderId, Guid? recipientId,
            string text, string language, DateTime utcNow) : base(id)
        {
            if (string.IsNullOrWhiteSpace(text))
                throw new ArgumentException("A chat message cannot be empty.", nameof(text));

            // Канал і його адресат мають збігатись: клановий без клану чи приватний
            // без адресата — баг викликача, а не рішення гравця
            if ((channel == ChatChannel.Clan) != (clanId is not null))
                throw new ArgumentException("Only a clan message has a clan.", nameof(clanId));

            if ((channel == ChatChannel.Private) != (recipientId is not null))
                throw new ArgumentException("Only a private message has a recipient.", nameof(recipientId));

            ServerId = serverId;
            Channel = channel;
            ClanId = clanId;
            SenderId = senderId;
            RecipientId = recipientId;
            Text = text;
            Language = language;
            SentAt = utcNow;

            RaiseDomainEvent(new ChatMessageSent(id, serverId, channel, clanId, senderId, recipientId, utcNow));
        }

        protected ChatMessage() { } // Для EF Core
    }
}
