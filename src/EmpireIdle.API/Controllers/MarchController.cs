using EmpireIdle.API.DTOs;
using EmpireIdle.Application.Marches.Commands;
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

        [HttpPost("{playerId:guid}/{marchId:guid}/speedup")]
        [ProducesResponseType(StatusCodes.Status204NoContent)]
        [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
        public async Task<IActionResult> SpeedUpMarch(Guid playerId, Guid marchId, CancellationToken cancellationToken)
        {
            await _mediator.Send(new SpeedUpMarchCommand(playerId, marchId), cancellationToken);
            return NoContent();
        }
    }
}