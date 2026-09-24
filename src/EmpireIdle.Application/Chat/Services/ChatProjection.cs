using EmpireIdle.Application.Chat.Contracts;
using EmpireIdle.Application.Interfaces;
using EmpireIdle.Domain.Entities;

namespace EmpireIdle.Application.Chat.Services
{
    /// <summary>
    /// Повідомлення у view для читача: імена відправників і переклади на його
    /// мову — по одному запиту на сторінку. Переклад лише читається з кешу:
    /// запит нічого не пише, кеш наповнює підписник на ChatMessageSent.
    /// </summary>
    public class ChatProjection
    {
        private readonly IChatRepository _chat;
        private readonly IPlayerRepository _players;

        public ChatProjection(IChatRepository chat, IPlayerRepository players)
        {
            _chat = chat;
            _players = players;
        }

        public async Task<List<ChatMessageView>> ProjectAsync(IReadOnlyCollection<ChatMessage> messages, Guid viewerId,
            string viewerLanguage, CancellationToken cancellationToken)
        {
            if (messages.Count == 0)
                return [];

            var names = await _players.GetNamesAsync(
                messages.Select(m => m.SenderId).Distinct().ToList(), cancellationToken);

            var foreign = messages.Where(m => m.Language != viewerLanguage).Select(m => m.Id).ToList();

            var translations = foreign.Count == 0
                ? new Dictionary<Guid, string>()
                : await _chat.GetTranslationsAsync(foreign, viewerLanguage, cancellationToken);

            return messages
                .Select(m => new ChatMessageView(
                    m.Id,
                    m.Channel.ToString(),
                    m.SenderId,
                    names.GetValueOrDefault(m.SenderId, "?"),
                    m.RecipientId,
                    m.Text,
                    m.Language,
                    translations.GetValueOrDefault(m.Id),
                    m.SentAt,
                    m.SenderId == viewerId))
                .ToList();
        }
    }
}
