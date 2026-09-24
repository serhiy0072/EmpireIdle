using EmpireIdle.Application.Interfaces;
using EmpireIdle.Domain.Services;
using MediatR;
using Microsoft.Extensions.Logging;

namespace EmpireIdle.Application.Chat.Commands
{
    /// <summary>Прибирає історію чату поточного світу, старшу за RetentionDays. Системна команда джоба.</summary>
    public record DeleteOldChatMessagesCommand : IRequest;

    public sealed class DeleteOldChatMessagesCommandHandler : IRequestHandler<DeleteOldChatMessagesCommand>
    {
        private readonly IChatRepository _chat;
        private readonly GameCatalog _catalog;
        private readonly TimeProvider _timeProvider;
        private readonly ILogger<DeleteOldChatMessagesCommandHandler> _logger;

        public DeleteOldChatMessagesCommandHandler(IChatRepository chat, GameCatalog catalog, TimeProvider timeProvider,
            ILogger<DeleteOldChatMessagesCommandHandler> logger)
        {
            _chat = chat;
            _catalog = catalog;
            _timeProvider = timeProvider;
            _logger = logger;
        }

        public async Task Handle(DeleteOldChatMessagesCommand request, CancellationToken cancellationToken)
        {
            var before = _timeProvider.GetUtcNow().UtcDateTime.AddDays(-_catalog.Config.Chat.RetentionDays);

            var deleted = await _chat.DeleteOlderThanAsync(before, cancellationToken);

            _logger.LogInformation("Deleted {Count} chat messages older than {Before:O}", deleted, before);
        }
    }
}
