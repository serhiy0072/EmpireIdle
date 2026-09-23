using EmpireIdle.API.DTOs;
using EmpireIdle.Application.Dungeons.Commands;
using EmpireIdle.Application.Dungeons.Contracts;
using EmpireIdle.Application.Dungeons.Queries;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace EmpireIdle.API.Controllers
{
    /// <summary>Данжі: вітрина, забіг і покрокові ходи.</summary>
    [ApiController]
    [Authorize]
    [Route("api/dungeons")]
    public class DungeonsController : ControllerBase
    {
        private readonly IMediator _mediator;

        public DungeonsController(IMediator mediator)
        {
            _mediator = mediator;
        }

        /// <summary>Вітрина: енергія, гейти, рівні й нагороди.</summary>
        [HttpGet("{playerId:guid}")]
        [ProducesResponseType(typeof(DungeonsOverview), StatusCodes.Status200OK)]
        public async Task<ActionResult<DungeonsOverview>> GetDungeons(Guid playerId, CancellationToken cancellationToken)
            => Ok(await _mediator.Send(new GetDungeonsQuery(playerId), cancellationToken));

        /// <summary>Незавершений забіг із повним станом бою; 204 — забігу немає.</summary>
        [HttpGet("{playerId:guid}/run")]
        [ProducesResponseType(typeof(DungeonRunView), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status204NoContent)]
        public async Task<ActionResult<DungeonRunView>> GetRun(Guid playerId, CancellationToken cancellationToken)
        {
            var run = await _mediator.Send(new GetDungeonRunQuery(playerId), cancellationToken);

            return run is null ? NoContent() : Ok(run);
        }

        /// <summary>
        /// Починає забіг: списує енергію й ставить першу хвилю.
        /// Ідемпотентна за заголовком Idempotency-Key.
        /// </summary>
        [HttpPost("{playerId:guid}/run")]
        [ProducesResponseType(typeof(DungeonRunView), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
        [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
        public async Task<ActionResult<DungeonRunView>> StartRun(
            Guid playerId, [FromBody] StartDungeonRunRequest request, CancellationToken cancellationToken)
        {
            await _mediator.Send(new StartDungeonRunCommand(playerId, request.DungeonKey, request.Level, request.HeroIds), cancellationToken);

            // Повертаємо одразу повний стан бою: клієнту не треба другого запиту, щоб почати хід
            var run = await _mediator.Send(new GetDungeonRunQuery(playerId), cancellationToken);

            return Ok(run);
        }

        /// <summary>
        /// Один хід. auto=true — хід обирає політика автобою й прокручуються
        /// ходи ворогів; інакше потрібні ability й target.
        /// </summary>
        [HttpPost("{playerId:guid}/run/{runId:guid}/turn")]
        [ProducesResponseType(typeof(DungeonTurnResponse), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
        [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
        public async Task<ActionResult<DungeonTurnResponse>> TakeTurn(
            Guid playerId, Guid runId, [FromBody] DungeonTurnRequest request, CancellationToken cancellationToken)
        {
            var result = await _mediator.Send(
                new TakeDungeonTurnCommand(playerId, runId, request.Auto, request.AbilityKey, request.TargetIndex),
                cancellationToken);

            return Ok(DungeonTurnResponse.From(result));
        }

        /// <summary>Вийти із забігу. Енергія не повертається. Ідемпотентна.</summary>
        [HttpPost("{playerId:guid}/run/{runId:guid}/abandon")]
        [ProducesResponseType(StatusCodes.Status204NoContent)]
        [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
        public async Task<IActionResult> Abandon(Guid playerId, Guid runId, CancellationToken cancellationToken)
        {
            await _mediator.Send(new AbandonDungeonRunCommand(playerId, runId), cancellationToken);

            return NoContent();
        }
    }
}
