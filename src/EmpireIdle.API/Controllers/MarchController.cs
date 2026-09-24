using EmpireIdle.API.DTOs;
using EmpireIdle.Application.Marches.Commands;
using EmpireIdle.Application.Marches.Queries;
using EmpireIdle.Domain.Combat;
using EmpireIdle.Domain.ValueObjects;
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

        /// <summary>Активні походи гравця: у дорозі до цілі або додому, найближче прибуття — першим.</summary>
        [HttpGet("{playerId:guid}")]
        [ProducesResponseType(typeof(List<MarchResponse>), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
        public async Task<ActionResult<List<MarchResponse>>> GetMarches(Guid playerId, CancellationToken cancellationToken)
        {
            var marches = await _mediator.Send(new GetMarchesQuery(playerId), cancellationToken);

            var response = marches
                .Select(m => new MarchResponse(
                    m.Id, m.TargetType, m.TargetId, m.TargetName, m.TargetLevel, m.TargetX, m.TargetY, m.Intent, m.State,
                    m.HeroId, m.DepartedAt, m.ArrivesAt,
                    m.Units.Select(u => new MarchUnitResponse(u.UnitType, u.Level, u.Count)).ToList(),
                    m.SpeedUpCostGems))
                .ToList();

            return Ok(response);
        }

        /// <summary>Відправити армію до цілі: в атаку на монстра або підкріпленням до села союзника.</summary>
        [HttpPost("{playerId:guid}")]
        [ProducesResponseType(typeof(Guid), StatusCodes.Status201Created)]
        [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
        public async Task<IActionResult> SendMarch(Guid playerId, [FromBody] SendMarchRequest request, CancellationToken cancellationToken)
        {
            var units = request.Units.ToDictionary(kv => UnitStackKey.Parse(kv.Key), kv => kv.Value);

            var marchId = await _mediator.Send(
                new SendMarchCommand(playerId, request.TargetType, request.TargetId, units, request.HeroId, request.Intent),
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
            var units = request.Units.ToDictionary(kv => UnitStackKey.Parse(kv.Key), kv => kv.Value);

            var preview = await _mediator.Send(
                new GetBattlePreviewQuery(playerId, request.TargetType, request.TargetId, request.HeroId, units),
                cancellationToken);

            return Ok(preview);
        }
    }
}
