using EmpireIdle.API.DTOs;
using EmpireIdle.Application.Chat.Commands;
using EmpireIdle.Application.Chat.Contracts;
using EmpireIdle.Application.Chat.Queries;
using EmpireIdle.Domain.Enums;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace EmpireIdle.API.Controllers
{
    /// <summary>Чат (GDD §7.3): історія каналів, приватні розмови й надсилання. Нове приходить через SignalR.</summary>
    [ApiController]
    [Authorize]
    [Route("api/chat")]
    public class ChatController : ControllerBase
    {
        private readonly IMediator _mediator;

        public ChatController(IMediator mediator)
        {
            _mediator = mediator;
        }

        /// <summary>
        /// Історія каналу до моменту before (без нього — найсвіжіше), старші першими.
        /// Для приватного каналу — partnerId співрозмовника.
        /// </summary>
        [HttpGet("{playerId:guid}/{channel}")]
        [ProducesResponseType(typeof(IReadOnlyList<ChatMessageView>), StatusCodes.Status200OK)]
        public async Task<ActionResult<IReadOnlyList<ChatMessageView>>> History(Guid playerId, ChatChannel channel,
            [FromQuery] Guid? partnerId, [FromQuery] DateTime? before, [FromQuery] int take = 50,
            CancellationToken cancellationToken = default)
            => Ok(await _mediator.Send(new GetChatHistoryQuery(playerId, channel, partnerId, before, take), cancellationToken));

        /// <summary>Приватні розмови гравця, найсвіжіші першими.</summary>
        [HttpGet("{playerId:guid}/conversations")]
        [ProducesResponseType(typeof(IReadOnlyList<ChatConversationView>), StatusCodes.Status200OK)]
        public async Task<ActionResult<IReadOnlyList<ChatConversationView>>> Conversations(Guid playerId,
            CancellationToken cancellationToken)
            => Ok(await _mediator.Send(new GetChatConversationsQuery(playerId), cancellationToken));

        /// <summary>Надіслати повідомлення. Ідемпотентна за заголовком Idempotency-Key.</summary>
        [HttpPost("{playerId:guid}/messages")]
        [ProducesResponseType(typeof(ChatMessageSentResponse), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
        [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
        public async Task<ActionResult<ChatMessageSentResponse>> Send(Guid playerId, [FromBody] SendChatMessageRequest request,
            CancellationToken cancellationToken)
        {
            var id = await _mediator.Send(new SendChatMessageCommand(playerId, request.Channel, request.RecipientId, request.Text),
                cancellationToken);

            return Ok(new ChatMessageSentResponse(id));
        }
    }
}
