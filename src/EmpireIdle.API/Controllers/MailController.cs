using EmpireIdle.Application.Mail.Commands;
using EmpireIdle.Application.Mail.Contracts;
using EmpireIdle.Application.Mail.Queries;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace EmpireIdle.API.Controllers
{
    /// <summary>Скринька (GDD §7.4): особисті листи, оголошення світу й позначки прочитання.</summary>
    [ApiController]
    [Authorize]
    [Route("api/mail")]
    public class MailController : ControllerBase
    {
        private readonly IMediator _mediator;

        public MailController(IMediator mediator)
        {
            _mediator = mediator;
        }

        /// <summary>Скринька з актуальним станом того, на що посилаються листи.</summary>
        [HttpGet("{playerId:guid}")]
        [ProducesResponseType(typeof(MailboxView), StatusCodes.Status200OK)]
        public async Task<ActionResult<MailboxView>> Mailbox(Guid playerId, CancellationToken cancellationToken)
            => Ok(await _mediator.Send(new GetMailboxQuery(playerId), cancellationToken));

        /// <summary>Лічильник непрочитаних — для бейджа.</summary>
        [HttpGet("{playerId:guid}/unread")]
        [ProducesResponseType(typeof(int), StatusCodes.Status200OK)]
        public async Task<ActionResult<int>> Unread(Guid playerId, CancellationToken cancellationToken)
            => Ok(await _mediator.Send(new GetMailUnreadQuery(playerId), cancellationToken));

        /// <summary>Позначити лист прочитаним. Ідемпотентна.</summary>
        [HttpPost("{playerId:guid}/letters/{letterId:guid}/read")]
        [ProducesResponseType(StatusCodes.Status204NoContent)]
        [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
        public async Task<IActionResult> ReadLetter(Guid playerId, Guid letterId, CancellationToken cancellationToken)
        {
            await _mediator.Send(new MarkLetterReadCommand(playerId, letterId), cancellationToken);
            return NoContent();
        }

        /// <summary>Позначити оголошення прочитаним. Ідемпотентна.</summary>
        [HttpPost("{playerId:guid}/announcements/{announcementId:guid}/read")]
        [ProducesResponseType(StatusCodes.Status204NoContent)]
        [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
        public async Task<IActionResult> ReadAnnouncement(Guid playerId, Guid announcementId, CancellationToken cancellationToken)
        {
            await _mediator.Send(new MarkAnnouncementReadCommand(playerId, announcementId), cancellationToken);
            return NoContent();
        }
    }
}
