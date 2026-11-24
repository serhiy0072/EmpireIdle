using EmpireIdle.API.DTOs;
using EmpireIdle.Application.Beasts.Commands;
using EmpireIdle.Application.Beasts.Queries;
using EmpireIdle.Application.Beasts.ReadModels;
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
        [ProducesResponseType(typeof(BeastPenView), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
        public async Task<ActionResult<BeastPenView>> GetPen(Guid playerId, CancellationToken cancellationToken)
        {
            return Ok(await _mediator.Send(new GetBeastPenQuery(playerId), cancellationToken));
        }

        /// <summary>Найближчий до села монстр, з якого виходить звір: кнопка «Знайти» у звіринці.</summary>
        [HttpGet("{playerId:guid}/{beastKey}/nearest")]
        [ProducesResponseType(typeof(NearestTameTargetResponse), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
        public async Task<ActionResult<NearestTameTargetResponse>> GetNearest(Guid playerId, string beastKey,
            CancellationToken cancellationToken)
        {
            var target = await _mediator.Send(new GetNearestTameTargetQuery(playerId, beastKey), cancellationToken);

            return Ok(new NearestTameTargetResponse(target is null
                ? null
                : new TameTargetResponse(target.MonsterId, target.MonsterKey, target.Level, target.X, target.Y)));
        }

        /// <summary>Нагодувати звіра кормом: рівень росте до стелі рангу, зайвий корм лишається в інвентарі.</summary>
        [HttpPost("{playerId:guid}/{beastKey}/feed")]
        [ProducesResponseType(typeof(BeastFedView), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
        public async Task<ActionResult<BeastFedView>> Feed(Guid playerId, string beastKey, [FromQuery] int count,
            CancellationToken cancellationToken)
        {
            return Ok(await _mediator.Send(new FeedBeastCommand(playerId, beastKey, count), cancellationToken));
        }

        /// <summary>Увімкнути пасивку звіра за їжу зі складу; далі — перезарядка.</summary>
        [HttpPost("{playerId:guid}/{beastKey}/activate")]
        [ProducesResponseType(typeof(BeastActivatedView), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
        public async Task<ActionResult<BeastActivatedView>> Activate(Guid playerId, string beastKey,
            CancellationToken cancellationToken)
        {
            return Ok(await _mediator.Send(new ActivateBeastCommand(playerId, beastKey), cancellationToken));
        }
    }
}
