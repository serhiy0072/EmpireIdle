using EmpireIdle.Application.Common.Security;
using EmpireIdle.Application.Interfaces;
using EmpireIdle.Domain.Entities;
using EmpireIdle.Domain.Enums;
using EmpireIdle.Domain.Exceptions;
using EmpireIdle.Domain.Services;
using MediatR;
using Microsoft.Extensions.Logging;

namespace EmpireIdle.Application.Chat.Commands
{
    /// <summary>
    /// Надіслати повідомлення в чат (GDD §7.3). RecipientId — лише для
    /// приватного каналу; клан береться з гравця, а не з запиту.
    /// </summary>
    /// <returns>Id повідомлення.</returns>
    public record SendChatMessageCommand(Guid PlayerId, ChatChannel Channel, Guid? RecipientId, string Text)
        : IRequest<Guid>, IPlayerScopedRequest, IIdempotentRequest;

    /// <summary>
    /// Перевіряє антиспам, довжину й право писати в канал, зберігає
    /// повідомлення. Доставку й переклад робить підписник на ChatMessageSent —
    /// після коміту, щоб не розіслати те, що база відкотила.
    /// </summary>
    public sealed class SendChatMessageCommandHandler : IRequestHandler<SendChatMessageCommand, Guid>
    {
        private readonly IChatRepository _chat;
        private readonly IPlayerRepository _players;
        private readonly IServerContext _serverContext;
        private readonly GameCatalog _catalog;
        private readonly IUnitOfWork _unitOfWork;
        private readonly TimeProvider _timeProvider;
        private readonly ILogger<SendChatMessageCommandHandler> _logger;

        public SendChatMessageCommandHandler(
            IChatRepository chat,
            IPlayerRepository players,
            IServerContext serverContext,
            GameCatalog catalog,
            IUnitOfWork unitOfWork,
            TimeProvider timeProvider,
            ILogger<SendChatMessageCommandHandler> logger)
        {
            _chat = chat;
            _players = players;
            _serverContext = serverContext;
            _catalog = catalog;
            _unitOfWork = unitOfWork;
            _timeProvider = timeProvider;
            _logger = logger;
        }

        public async Task<Guid> Handle(SendChatMessageCommand request, CancellationToken cancellationToken)
        {
            var now = _timeProvider.GetUtcNow().UtcDateTime;
            var chat = _catalog.Config.Chat;

            var text = request.Text.Trim();

            if (text.Length > chat.MaxLength)
                throw new RequirementNotMetException(RefusalReasons.ChatTooLong,
                    $"Message of {text.Length} characters exceeds {chat.MaxLength}.", chat.MaxLength);

            var sender = await _players.GetByIdAsync(request.PlayerId, cancellationToken)
                ?? throw new EntityNotFoundException("Player", request.PlayerId.ToString());

            await EnsureNotFloodingAsync(sender.Id, now, cancellationToken);

            Guid? clanId = null;

            switch (request.Channel)
            {
                case ChatChannel.Clan:
                    // Членство — з бази, не з клієнта: інакше писали б у чужий клан
                    clanId = sender.ClanId
                        ?? throw new RequirementNotMetException(RefusalReasons.ChatNoClan, $"Player {sender.Id} is not in a clan.");
                    break;

                case ChatChannel.Private:
                {
                    var recipientId = request.RecipientId ?? throw new EntityNotFoundException("Player", "(none)");

                    if (recipientId == sender.Id)
                        throw new RequirementNotMetException(RefusalReasons.ChatToSelf, "A player cannot message themselves.");

                    // Адресат з іншого світу не знайдеться: query-фільтр тримає поточний
                    _ = await _players.GetByIdAsync(recipientId, cancellationToken)
                        ?? throw new EntityNotFoundException("Player", recipientId.ToString());
                    break;
                }
            }

            var message = new ChatMessage(Guid.NewGuid(), _serverContext.ServerId, request.Channel, clanId, sender.Id,
                request.Channel == ChatChannel.Private ? request.RecipientId : null, text, sender.Language, now);

            await _chat.AddAsync(message, cancellationToken);
            await _unitOfWork.SaveChangesAsync(cancellationToken);

            _logger.LogInformation("Player {PlayerId} wrote to {Channel} chat", sender.Id, request.Channel);

            return message.Id;
        }

        /// <summary>
        /// Антиспам: не більше RateLimitCount повідомлень за вікно. Відмова
        /// каже, скільки секунд чекати, — до виходу найстарішого з вікна.
        /// </summary>
        private async Task EnsureNotFloodingAsync(Guid senderId, DateTime now, CancellationToken cancellationToken)
        {
            var chat = _catalog.Config.Chat;
            var window = TimeSpan.FromSeconds(chat.RateLimitWindowSeconds);
            var since = now - window;

            if (await _chat.CountSentSinceAsync(senderId, since, cancellationToken) < chat.RateLimitCount)
                return;

            var oldest = await _chat.GetOldestSentSinceAsync(senderId, since, cancellationToken) ?? now;
            var wait = Math.Max(1, (int)Math.Ceiling((oldest + window - now).TotalSeconds));

            throw new RequirementNotMetException(RefusalReasons.ChatTooFast,
                $"Player {senderId} sends messages too fast; wait {wait}s.", wait);
        }
    }
}
