using EmpireIdle.API.DTOs;
using EmpireIdle.Application.Beasts.Queries;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace EmpireIdle.API.Controllers
{
    /// <summary>Звіринець (GDD §5.10). Приручають маршем із наміром Tame — через MarchController.</summary>
    [ApiController]
    [Authorize]
    [Route("api/beasts")]
    public class BeastsController : ControllerBase
    {
        private readonly IMediator _mediator;

        public BeastsController(IMediator mediator) => _mediator = mediator;

        [HttpGet("{playerId:guid}")]
        [ProducesResponseType(typeof(BeastPenResponse), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
        public async Task<ActionResult<BeastPenResponse>> GetPen(Guid playerId, CancellationToken cancellationToken)
        {
            var pen = await _mediator.Send(new GetBeastPenQuery(playerId), cancellationToken);

            return Ok(new BeastPenResponse(
                pen.Capacity,
                pen.Beasts.Select(b => new BeastResponse(b.BeastKey, b.Rank, b.TamedAt)).ToList(),
                pen.Taming.Select(t => new BeastTamingResponse(t.BeastKey, t.MonsterKey, t.Chance, t.Misses, t.PityWins)).ToList()));
        }
    }
}
