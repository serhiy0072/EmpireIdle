using EmpireIdle.Application.Tutorial.Commands;
using EmpireIdle.Application.Tutorial.Queries;
using EmpireIdle.Application.Tutorial.ReadModels;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace EmpireIdle.API.Controllers
{
    /// <summary>
    /// Прогрес навчання. Сервер зберігає лише множину побачених кроків:
    /// зміст і порядок підказок — справа клієнта.
    /// </summary>
    [ApiController]
    [Authorize]
    [Route("api/tutorial")]
    public class TutorialController : ControllerBase
    {
        private readonly IMediator _mediator;

        public TutorialController(IMediator mediator) => _mediator = mediator;

        [HttpGet("{playerId:guid}")]
        [ProducesResponseType(typeof(TutorialProgressView), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
        public async Task<ActionResult<TutorialProgressView>> GetProgress(Guid playerId, CancellationToken cancellationToken)
        {
            return Ok(await _mediator.Send(new GetTutorialProgressQuery(playerId), cancellationToken));
        }

        /// <summary>Позначити крок побаченим. Повтор безпечний.</summary>
        [HttpPost("{playerId:guid}/steps/{stepKey}")]
        [ProducesResponseType(StatusCodes.Status204NoContent)]
        [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
        public async Task<IActionResult> MarkSeen(Guid playerId, string stepKey, CancellationToken cancellationToken)
        {
            await _mediator.Send(new MarkTutorialStepSeenCommand(playerId, stepKey), cancellationToken);
            return NoContent();
        }

        /// <summary>Відмовитись від веденого туторіалу.</summary>
        [HttpPost("{playerId:guid}/skip")]
        [ProducesResponseType(StatusCodes.Status204NoContent)]
        public async Task<IActionResult> Skip(Guid playerId, CancellationToken cancellationToken)
        {
            await _mediator.Send(new SkipTutorialCommand(playerId), cancellationToken);
            return NoContent();
        }
    }
}
