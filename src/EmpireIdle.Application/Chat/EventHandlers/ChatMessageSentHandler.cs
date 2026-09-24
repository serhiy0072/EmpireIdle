using EmpireIdle.Application.Chat.Contracts;
using EmpireIdle.Application.Common.Events;
using EmpireIdle.Application.Interfaces;
using EmpireIdle.Domain.Entities;
using EmpireIdle.Domain.Enums;
using EmpireIdle.Domain.Events;
using EmpireIdle.Domain.Services;
using MediatR;
using Microsoft.Extensions.Logging;

namespace EmpireIdle.Application.Chat.EventHandlers
{
    /// <summary>
    /// Нове повідомлення: переклад на мови світу, потім доставка.
    ///
    /// Переклад тут, а не в запиті історії: запит нічого не пише (CQRS),
    /// а мов у конфігу кілька — переклад на всі одразу дешевший за
    /// перевірку «хто це читатиме».
    ///
    /// Адресати кланового каналу читаються з бази саме зараз (GDD §7.3):
    /// членство змінюється частіше, ніж живуть групи хабу.
    /// </summary>
    public sealed class ChatMessageSentHandler : INotificationHandler<DomainEventNotification<ChatMessageSent>>
    {
        private readonly IChatRepository _chat;
        private readonly IPlayerRepository _players;
        private readonly ITranslator _translator;
        private readonly IGameNotifier _notifier;
        private readonly IUnitOfWork _unitOfWork;
        private readonly GameCatalog _catalog;
        private readonly ILogger<ChatMessageSentHandler> _logger;

        public ChatMessageSentHandler(
            IChatRepository chat,
            IPlayerRepository players,
            ITranslator translator,
            IGameNotifier notifier,
            IUnitOfWork unitOfWork,
            GameCatalog catalog,
            ILogger<ChatMessageSentHandler> logger)
        {
            _chat = chat;
            _players = players;
            _translator = translator;
            _notifier = notifier;
            _unitOfWork = unitOfWork;
            _catalog = catalog;
            _logger = logger;
        }

        public async Task Handle(DomainEventNotification<ChatMessageSent> notification, CancellationToken cancellationToken)
        {
            var e = notification.DomainEvent;

            var message = await _chat.GetByIdAsync(e.MessageId, cancellationToken);

            // Повідомлення вже прибрав джоб історії — доставляти нема чого
            if (message is null)
                return;

            var translations = await TranslateAsync(message, cancellationToken);

            var names = await _players.GetNamesAsync([message.SenderId], cancellationToken);

            var notice = new ChatMessageNotice(message.Id, message.Channel.ToString(), message.SenderId,
                names.GetValueOrDefault(message.SenderId, "?"), message.ClanId, message.RecipientId, message.Text,
                message.Language, translations, message.SentAt);

            switch (message.Channel)
            {
                case ChatChannel.Server:
                    await _notifier.NotifyChatToServerAsync(message.ServerId, notice, cancellationToken);
                    break;

                case ChatChannel.Clan:
                    var members = await _players.GetIdsByClanAsync(message.ClanId!.Value, cancellationToken);
                    await _notifier.NotifyChatToPlayersAsync(members, notice, cancellationToken);
                    break;

                case ChatChannel.Private:
                    await _notifier.NotifyChatToPlayersAsync([message.SenderId, message.RecipientId!.Value], notice,
                        cancellationToken);
                    break;
            }
        }

        /// <summary>
        /// Переклади на всі мови світу, крім мови самого повідомлення.
        /// Провайдера немає — порожньо, і гравці бачать оригінал.
        /// </summary>
        private async Task<IReadOnlyDictionary<string, string>> TranslateAsync(ChatMessage message,
            CancellationToken cancellationToken)
        {
            var result = new Dictionary<string, string>();

            if (!_translator.IsAvailable)
                return result;

            foreach (var language in _catalog.Config.Localization.SupportedLanguages.Where(l => l != message.Language))
            {
                try
                {
                    var text = await _translator.TranslateAsync(message.Text, message.Language, language, cancellationToken);

                    if (text is null)
                        continue;

                    result[language] = text;
                    await _chat.AddTranslationAsync(new ChatTranslation(message.Id, language, text), cancellationToken);
                }
                catch (Exception ex) when (ex is not OperationCanceledException)
                {
                    // Збій провайдера не має зупиняти доставку: без перекладу — оригінал
                    _logger.LogWarning(ex, "Translation of message {MessageId} to {Language} failed", message.Id, language);
                }
            }

            if (result.Count > 0)
                await _unitOfWork.SaveChangesAsync(cancellationToken);

            return result;
        }
    }
}
