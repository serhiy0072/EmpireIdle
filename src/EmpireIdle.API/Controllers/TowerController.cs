using EmpireIdle.API.DTOs;
using EmpireIdle.API.Swagger;
using EmpireIdle.Application.Battles.ReadModels;
using EmpireIdle.Application.Tower.Commands;
using EmpireIdle.Application.Tower.Queries;
using EmpireIdle.Application.Tower.ReadModels;
using EmpireIdle.Domain.Battles;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace EmpireIdle.API.Controllers
{
    /// <summary>«Тисячна вежа»: стан, штурм поверху й рейтинг сервера.</summary>
    [ApiController]
    [Authorize]
    [Route("api/tower")]
    public class TowerController : ControllerBase
    {
        private readonly IMediator _mediator;

        public TowerController(IMediator mediator)
        {
            _mediator = mediator;
        }

        /// <summary>Прогрес, наступний поверх зі складом ворога, нагорода найближчих поверхів і бафи.</summary>
        [HttpGet("{playerId:guid}")]
        [ProducesResponseType(typeof(TowerView), StatusCodes.Status200OK)]
        public async Task<ActionResult<TowerView>> GetTower(Guid playerId, CancellationToken cancellationToken)
            => Ok(await _mediator.Send(new GetTowerQuery(playerId), cancellationToken));

        /// <summary>
        /// Штурм поверху: сервер рахує бій до кінця й повертає лог для відтворення. Перемога закриває
        /// поверх і видає нагороду; програш нічого не коштує. Ідемпотентна за заголовком Idempotency-Key.
        /// </summary>
        [HttpPost("{playerId:guid}/storm")]
        [ProducesResponseType(typeof(TowerStormResult), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
        [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
        public async Task<ActionResult<TowerStormResult>> Storm(
            Guid playerId, [FromBody] StormTowerFloorRequest request, CancellationToken cancellationToken)
            => Ok(await _mediator.Send(new StormTowerFloorCommand(playerId, request.Floor, request.Lanes, Casts(request.Casts)),
                cancellationToken));

        /// <summary>
        /// Ручний бій: бій із наказами гравця від початку, без збереження. Кожне натискання вміння — новий
        /// перегляд; результат зараховує штурм із тими самими наказами.
        /// </summary>
        [HttpPost("{playerId:guid}/preview")]
        [WithoutIdempotencyKey]
        [ProducesResponseType(typeof(BattleReplay), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
        [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
        public async Task<ActionResult<BattleReplay>> Preview(
            Guid playerId, [FromBody] PreviewTowerFloorRequest request, CancellationToken cancellationToken)
            => Ok(await _mediator.Send(new PreviewTowerFloorQuery(playerId, request.Floor, request.Lanes, Casts(request.Casts) ?? []),
                cancellationToken));

        /// <summary>Рейтинг вежі на сервері й власний рядок гравця.</summary>
        [HttpGet("{playerId:guid}/leaderboard")]
        [ProducesResponseType(typeof(TowerLeaderboard), StatusCodes.Status200OK)]
        public async Task<ActionResult<TowerLeaderboard>> GetLeaderboard(Guid playerId, [FromQuery] int count = 100,
            CancellationToken cancellationToken = default)
            => Ok(await _mediator.Send(new GetTowerLeaderboardQuery(playerId, count), cancellationToken));

        private static List<ManualCast>? Casts(List<ManualCastRequest>? casts) => casts?.Select(c => new ManualCast(c.Tick, c.Lane)).ToList();
    }
}
