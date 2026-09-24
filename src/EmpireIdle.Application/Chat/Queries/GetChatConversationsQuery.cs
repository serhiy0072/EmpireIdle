using EmpireIdle.Application.Chat.Contracts;
using EmpireIdle.Application.Chat.Services;
using EmpireIdle.Application.Common.Security;
using EmpireIdle.Application.Interfaces;
using EmpireIdle.Domain.Exceptions;
using MediatR;

namespace EmpireIdle.Application.Chat.Queries
{
    /// <summary>Приватні розмови гравця: з ким і останнє повідомлення, найсвіжіші першими.</summary>
    public record GetChatConversationsQuery(Guid PlayerId) : IRequest<IReadOnlyList<ChatConversationView>>, IPlayerScopedRequest
    {
        public const int MaxConversations = 50;
    }

    public sealed class GetChatConversationsQueryHandler
        : IRequestHandler<GetChatConversationsQuery, IReadOnlyList<ChatConversationView>>
    {
        private readonly IChatRepository _chat;
        private readonly IPlayerRepository _players;
        private readonly ChatProjection _projection;

        public GetChatConversationsQueryHandler(IChatRepository chat, IPlayerRepository players, ChatProjection projection)
        {
            _chat = chat;
            _players = players;
            _projection = projection;
        }

        public async Task<IReadOnlyList<ChatConversationView>> Handle(GetChatConversationsQuery request,
            CancellationToken cancellationToken)
        {
            var viewer = await _players.GetByIdAsync(request.PlayerId, cancellationToken)
                ?? throw new EntityNotFoundException("Player", request.PlayerId.ToString());

            var latest = await _chat.GetLatestPrivateMessagesAsync(viewer.Id, GetChatConversationsQuery.MaxConversations,
                cancellationToken);

            var views = await _projection.ProjectAsync(latest, viewer.Id, viewer.Language, cancellationToken);

            var partners = latest
                .Select(m => m.SenderId == viewer.Id ? m.RecipientId!.Value : m.SenderId)
                .ToList();

            var names = await _players.GetNamesAsync(partners.Distinct().ToList(), cancellationToken);

            return views
                .Select((view, index) => new ChatConversationView(partners[index], names.GetValueOrDefault(partners[index], "?"), view))
                .ToList();
        }
    }
}
