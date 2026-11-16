using EmpireIdle.Application.Rating.Queries;
using EmpireIdle.Application.Rating.ReadModels;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace EmpireIdle.API.Controllers
{
    /// <summary>Серверний рейтинг.</summary>
    [ApiController]
    [Route("api/rating")]
    [Authorize]
    public class RatingController : ControllerBase
    {
        private readonly IMediator _mediator;

        public RatingController(IMediator mediator) => _mediator = mediator;

        /// <summary>
        /// Топ світу. Перераховується щогодини, тому дані можуть відставати —
        /// це свідомий розмін на відсутність зв'язності між силою й рейтингом.
        /// </summary>
        [HttpGet("top")]
        [ProducesResponseType(typeof(List<LeaderboardEntry>), StatusCodes.Status200OK)]
        public async Task<IActionResult> GetTop([FromQuery] int count = 100,
            CancellationToken cancellationToken = default)
        {
            return Ok(await _mediator.Send(new GetLeaderboardQuery(count), cancellationToken));
        }

        /// <summary>Місце гравця й з чого склався його рейтинг.</summary>
        [HttpGet("{playerId:guid}")]
        [ProducesResponseType(typeof(PlayerRankView), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
        public async Task<IActionResult> GetRank(Guid playerId, CancellationToken cancellationToken)
        {
            return Ok(await _mediator.Send(new GetPlayerRankQuery(playerId), cancellationToken));
        }
    }
}
