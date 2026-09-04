using EmpireIdle.API.DTOs;
using EmpireIdle.Application.Marches.Commands;
using EmpireIdle.Application.Marches.Queries;
using EmpireIdle.Domain.Services;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace EmpireIdle.API.Controllers
{
    /// <summary>Походи армії.</summary>
    [ApiController]
    [Authorize]
    [Route("api/marches")]
    public class MarchController : ControllerBase
    {
        private readonly IMediator _mediator;

        public MarchController(IMediator mediator)
        {
            _mediator = mediator;
        }

        /// <summary>Відправити армію до цілі.</summary>
        [HttpPost("{playerId:guid}")]
        [ProducesResponseType(typeof(Guid), StatusCodes.Status201Created)]
        [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
        public async Task<IActionResult> SendMarch(Guid playerId, [FromBody] SendMarchRequest request, CancellationToken cancellationToken)
        {
            var marchId = await _mediator.Send(
                new SendMarchCommand(playerId, request.TargetType, request.TargetId, request.Units),
                cancellationToken);

            return Created((string?)null, marchId);
        }


        /// <summary>
        /// Миттєво завершити переміщення армії за gems. Бій відбудеться
        /// найближчим проходом сканера.
        /// </summary>
        [HttpPost("{playerId:guid}/{marchId:guid}/speedup")]
        [ProducesResponseType(StatusCodes.Status204NoContent)]
        [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
        public async Task<IActionResult> SpeedUpMarch(Guid playerId, Guid marchId, CancellationToken cancellationToken)
        {
            await _mediator.Send(new SpeedUpMarchCommand(playerId, marchId), cancellationToken);
            return NoContent();
        }

        /// <summary>
        /// Оцінка бою до відправки. Повертає смугу шансів, не числа:
        /// точне співвідношення сил гравцю не показується.
        /// </summary>
        [HttpPost("{playerId:guid}/preview")]
        [ProducesResponseType(typeof(BattlePreviewResult), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
        [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
        public async Task<ActionResult<BattlePreviewResult>> PreviewBattle(Guid playerId,
            [FromBody] SendMarchRequest request, CancellationToken cancellationToken)
        {
            var preview = await _mediator.Send(
                new GetBattlePreviewQuery(playerId, request.TargetType, request.TargetId, request.Units),
                cancellationToken);

            return Ok(preview);
        }
    }
}
