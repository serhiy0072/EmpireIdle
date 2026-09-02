using EmpireIdle.API.DTOs;
using EmpireIdle.Application.ServerQuests.Queries;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace EmpireIdle.API.Controllers
{
    /// <summary>Серверні квести: спільні цілі світу.</summary>
    [ApiController]
    [Route("api/server-quests")]
    [Authorize]
    public class ServerQuestController : ControllerBase
    {
        private readonly IMediator _mediator;

        public ServerQuestController(IMediator mediator) => _mediator = mediator;

        /// <summary>
        /// Активні серверні квести з власним внеском і рангом гравця.
        /// Підсумок оновлюється джобом щохвилини, тому може трохи відставати.
        /// </summary>
        [HttpGet("{playerId:guid}")]
        [ProducesResponseType(typeof(List<ServerQuestResponse>), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
        public async Task<IActionResult> GetServerQuests(Guid playerId, CancellationToken cancellationToken)
        {
            var quests = await _mediator.Send(new GetServerQuestsQuery(playerId), cancellationToken);

            var response = quests
                .Select(q => new ServerQuestResponse(
                    q.Key, q.DisplayName, q.Total, q.Target, q.State.ToString(),
                    q.CompletedAt, q.MyContribution, q.MyRank))
                .ToList();

            return Ok(response);
        }
    }
}
