using EmpireIdle.API.DTOs;
using EmpireIdle.Application.Research.Commands;
using EmpireIdle.Application.Research.Queries;
using EmpireIdle.Application.Research.ReadModels;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace EmpireIdle.API.Controllers
{
    /// <summary>Академія: особисті технології гравця (рішення 28.10.2026).</summary>
    [ApiController]
    [Authorize]
    [Route("api/research")]
    public class ResearchController : ControllerBase
    {
        private readonly IMediator _mediator;

        public ResearchController(IMediator mediator)
        {
            _mediator = mediator;
        }

        /// <summary>Дерево всіх тірів із рівнями, ціною й часом наступного рівня, активне дослідження й сумарні бонуси.</summary>
        [HttpGet("{playerId:guid}")]
        [ProducesResponseType(typeof(ResearchView), StatusCodes.Status200OK)]
        public async Task<ActionResult<ResearchView>> GetResearch(Guid playerId, CancellationToken cancellationToken)
            => Ok(await _mediator.Send(new GetResearchQuery(playerId), cancellationToken));

        /// <summary>Почати наступний рівень вузла. Ідемпотентна за заголовком Idempotency-Key.</summary>
        [HttpPost("{playerId:guid}/start")]
        [ProducesResponseType(StatusCodes.Status204NoContent)]
        [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
        [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
        public async Task<IActionResult> Start(Guid playerId, [FromBody] StartResearchRequest request, CancellationToken cancellationToken)
        {
            await _mediator.Send(new StartResearchCommand(playerId, request.Tier, request.Node), cancellationToken);
            return NoContent();
        }

        /// <summary>Завершити поточне дослідження за gems. Ідемпотентна за заголовком Idempotency-Key.</summary>
        [HttpPost("{playerId:guid}/speedup")]
        [ProducesResponseType(StatusCodes.Status204NoContent)]
        [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
        public async Task<IActionResult> SpeedUp(Guid playerId, CancellationToken cancellationToken)
        {
            await _mediator.Send(new SpeedUpResearchCommand(playerId), cancellationToken);
            return NoContent();
        }
    }
}
