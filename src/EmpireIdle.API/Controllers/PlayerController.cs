using EmpireIdle.API.DTOs;
using EmpireIdle.Application.Chat.Commands;
using EmpireIdle.Application.Chat.Queries;
using EmpireIdle.Application.LoginRewards.Commands;
using EmpireIdle.Application.LoginRewards.Contracts;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace EmpireIdle.API.Controllers
{
    /// <summary>Налаштування гравця й вхід у гру.</summary>
    [ApiController]
    [Authorize]
    [Route("api/player")]
    public class PlayerController : ControllerBase
    {
        private readonly IMediator _mediator;

        public PlayerController(IMediator mediator)
        {
            _mediator = mediator;
        }

        [HttpGet("{playerId:guid}/settings")]
        [ProducesResponseType(typeof(PlayerSettingsView), StatusCodes.Status200OK)]
        public async Task<ActionResult<PlayerSettingsView>> Settings(Guid playerId, CancellationToken cancellationToken)
            => Ok(await _mediator.Send(new GetPlayerSettingsQuery(playerId), cancellationToken));

        /// <summary>Змінити мову інтерфейсу. Ідемпотентна.</summary>
        [HttpPost("{playerId:guid}/language")]
        [ProducesResponseType(StatusCodes.Status204NoContent)]
        [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
        public async Task<IActionResult> ChangeLanguage(Guid playerId, [FromBody] ChangeLanguageRequest request,
            CancellationToken cancellationToken)
        {
            await _mediator.Send(new ChangeLanguageCommand(playerId, request.Language), cancellationToken);
            return NoContent();
        }

        /// <summary>
        /// Вхід у гру: клієнт шле при відкритті й після опівночі за UTC.
        /// Нагороди за вхід лягають листами в скриньку. Ідемпотентна.
        /// </summary>
        [HttpPost("{playerId:guid}/check-in")]
        [ProducesResponseType(typeof(CheckInView), StatusCodes.Status200OK)]
        public async Task<ActionResult<CheckInView>> CheckIn(Guid playerId, CancellationToken cancellationToken)
            => Ok(await _mediator.Send(new CheckInCommand(playerId), cancellationToken));
    }
}
