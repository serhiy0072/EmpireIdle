using EmpireIdle.API.DTOs;
using EmpireIdle.Application.Rating.Queries;
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
        [ProducesResponseType(typeof(List<LeaderboardEntryResponse>), StatusCodes.Status200OK)]
        public async Task<IActionResult> GetTop([FromQuery] int count = 100,
            CancellationToken cancellationToken = default)
        {
            var entries = await _mediator.Send(new GetLeaderboardQuery(count), cancellationToken);

            var response = entries
                .Select(e => new LeaderboardEntryResponse(e.Rank, e.PlayerId, e.PlayerName, e.Rating, e.Power))
                .ToList();

            return Ok(response);
        }

        /// <summary>Місце гравця й з чого склався його рейтинг.</summary>
        [HttpGet("{playerId:guid}")]
        [ProducesResponseType(typeof(PlayerRankResponse), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
        public async Task<IActionResult> GetRank(Guid playerId, CancellationToken cancellationToken)
        {
            var rank = await _mediator.Send(new GetPlayerRankQuery(playerId), cancellationToken);

            var response = new PlayerRankResponse(
                rank.Rank, rank.Rating,
                rank.PowerScore, rank.DevelopmentScore, rank.ActivityScore,
                rank.MonstersDefeated, rank.BattlesWon, rank.QuestsCompleted,
                rank.UpdatedAt);

            return Ok(response);
        }
    }
}
