using EmpireIdle.Application.Chat.Contracts;
using EmpireIdle.Application.Chat.Services;
using EmpireIdle.Application.Common.Security;
using EmpireIdle.Application.Interfaces;
using EmpireIdle.Domain.Enums;
using EmpireIdle.Domain.Exceptions;
using MediatR;

namespace EmpireIdle.Application.Chat.Queries
{
    /// <summary>
    /// Історія каналу до моменту Before (null — найсвіжіше), старші першими.
    /// PartnerId — співрозмовник для приватного каналу.
    /// </summary>
    public record GetChatHistoryQuery(Guid PlayerId, ChatChannel Channel, Guid? PartnerId, DateTime? Before, int Take)
        : IRequest<IReadOnlyList<ChatMessageView>>, IPlayerScopedRequest
    {
        public const int MaxTake = 100;
    }

    public sealed class GetChatHistoryQueryHandler : IRequestHandler<GetChatHistoryQuery, IReadOnlyList<ChatMessageView>>
    {
        private readonly IChatRepository _chat;
        private readonly IPlayerRepository _players;
        private readonly ChatProjection _projection;

        public GetChatHistoryQueryHandler(IChatRepository chat, IPlayerRepository players, ChatProjection projection)
        {
            _chat = chat;
            _players = players;
            _projection = projection;
        }

        public async Task<IReadOnlyList<ChatMessageView>> Handle(GetChatHistoryQuery request, CancellationToken cancellationToken)
        {
            var take = Math.Clamp(request.Take, 1, GetChatHistoryQuery.MaxTake);

            var viewer = await _players.GetByIdAsync(request.PlayerId, cancellationToken)
                ?? throw new EntityNotFoundException("Player", request.PlayerId.ToString());

            var messages = request.Channel switch
            {
                ChatChannel.Server => await _chat.GetServerHistoryAsync(request.Before, take, cancellationToken),

                // Без клану кланового чату немає — порожньо, а не помилка: вкладка просто пуста
                ChatChannel.Clan => viewer.ClanId is { } clanId
                    ? await _chat.GetClanHistoryAsync(clanId, request.Before, take, cancellationToken)
                    : [],

                ChatChannel.Private => request.PartnerId is { } partner
                    ? await _chat.GetPrivateHistoryAsync(viewer.Id, partner, request.Before, take, cancellationToken)
                    : [],

                _ => []
            };

            return await _projection.ProjectAsync(messages, viewer.Id, viewer.Language, cancellationToken);
        }
    }
}
