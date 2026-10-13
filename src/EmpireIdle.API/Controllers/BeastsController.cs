using EmpireIdle.API.DTOs;
using EmpireIdle.Application.Beasts.Commands;
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
                pen.Beasts.Select(b => new BeastResponse(
                    b.BeastKey, b.Rank, b.Level, b.Experience, b.ExperienceToNext, b.MaxLevel, b.TamedAt,
                    new BeastPassiveResponse(b.Passive.Effect, b.Passive.Bonus, b.Passive.DurationMinutes, b.Passive.CooldownMinutes,
                        b.Passive.ActivationFood, b.Passive.ActiveUntil, b.Passive.CooldownUntil))).ToList(),
                pen.Taming.Select(t => new BeastTamingResponse(t.BeastKey, t.MonsterKey, t.Chance, t.Misses, t.PityWins)).ToList()));
        }

        /// <summary>Нагодувати звіра кормом: рівень росте до стелі рангу, зайвий корм лишається в інвентарі.</summary>
        [HttpPost("{playerId:guid}/{beastKey}/feed")]
        [ProducesResponseType(typeof(BeastFedResponse), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
        public async Task<ActionResult<BeastFedResponse>> Feed(Guid playerId, string beastKey, [FromQuery] int count,
            CancellationToken cancellationToken)
        {
            var fed = await _mediator.Send(new FeedBeastCommand(playerId, beastKey, count), cancellationToken);

            return Ok(new BeastFedResponse(fed.BeastKey, fed.Level, fed.Experience, fed.Eaten));
        }

        /// <summary>Увімкнути пасивку звіра за їжу зі складу; далі — перезарядка.</summary>
        [HttpPost("{playerId:guid}/{beastKey}/activate")]
        [ProducesResponseType(typeof(BeastActivatedResponse), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
        public async Task<ActionResult<BeastActivatedResponse>> Activate(Guid playerId, string beastKey,
            CancellationToken cancellationToken)
        {
            var activated = await _mediator.Send(new ActivateBeastCommand(playerId, beastKey), cancellationToken);

            return Ok(new BeastActivatedResponse(activated.BeastKey, activated.ActiveUntil, activated.CooldownUntil));
        }
    }
}
