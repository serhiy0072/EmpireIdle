using EmpireIdle.API.DTOs;
using EmpireIdle.Application.Clans.Commands;
using EmpireIdle.Application.Clans.Queries;
using EmpireIdle.Application.Clans.ReadModels;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace EmpireIdle.API.Controllers
{
    /// <summary>
    /// Заявки на вступ і запрошення в клан.
    ///
    /// Окремо від ClanController: у них власний життєвий цикл із чотирьох
    /// станів, і разом зі складом та допомогою це був би контролер
    /// на двадцять ендпоінтів.
    /// </summary>
    [ApiController]
    [Authorize]
    [Route("api/clans/{playerId:guid}/requests")]
    public class ClanRequestController : ControllerBase
    {
        private readonly IMediator _mediator;

        public ClanRequestController(IMediator mediator) => _mediator = mediator;

        /// <summary>Заявки, що чекають рішення. Потрібен дозвіл Recruit.</summary>
        [HttpGet]
        [ProducesResponseType(typeof(List<ClanApplicationItem>), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
        public async Task<IActionResult> GetApplications(Guid playerId, CancellationToken cancellationToken)
        {
            return Ok(await _mediator.Send(new GetClanApplicationsQuery(playerId), cancellationToken));
        }

        /// <summary>Запрошення, адресовані гравцеві.</summary>
        [HttpGet("invites")]
        [ProducesResponseType(typeof(List<ClanInviteItem>), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
        public async Task<IActionResult> GetInvites(Guid playerId, CancellationToken cancellationToken)
        {
            return Ok(await _mediator.Send(new GetMyClanInvitesQuery(playerId), cancellationToken));
        }

        /// <summary>Запросити гравця в клан. Потрібен дозвіл Recruit.</summary>
        [HttpPost("invite/{targetPlayerId:guid}")]
        [ProducesResponseType(typeof(Guid), StatusCodes.Status201Created)]
        [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
        public async Task<IActionResult> Invite(Guid playerId, Guid targetPlayerId,
            CancellationToken cancellationToken)
        {
            var requestId = await _mediator.Send(
                new InviteToClanCommand(playerId, targetPlayerId), cancellationToken);

            return Created((string?)null, requestId);
        }

        /// <summary>
        /// Рішення по заявці або запрошенню: заявку вирішує офіцер,
        /// запрошення — той, кого запросили.
        /// </summary>
        [HttpPost("{requestId:guid}/resolve")]
        [ProducesResponseType(StatusCodes.Status204NoContent)]
        [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
        public async Task<IActionResult> Resolve(Guid playerId, Guid requestId,
            [FromBody] ResolveClanRequestRequest request, CancellationToken cancellationToken)
        {
            await _mediator.Send(
                new ResolveClanRequestCommand(playerId, requestId, request.Approve), cancellationToken);

            return NoContent();
        }

        /// <summary>Зняти власну заявку або відкликати надіслане запрошення.</summary>
        [HttpPost("{requestId:guid}/cancel")]
        [ProducesResponseType(StatusCodes.Status204NoContent)]
        [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
        public async Task<IActionResult> Cancel(Guid playerId, Guid requestId, CancellationToken cancellationToken)
        {
            await _mediator.Send(new CancelClanRequestCommand(playerId, requestId), cancellationToken);

            return NoContent();
        }
    }
}
