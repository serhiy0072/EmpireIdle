using EmpireIdle.Application.ServerQuests.Queries;
using EmpireIdle.Application.ServerQuests.ReadModels;
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
        [ProducesResponseType(typeof(List<ServerQuestView>), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
        public async Task<IActionResult> GetServerQuests(Guid playerId, CancellationToken cancellationToken)
        {
            return Ok(await _mediator.Send(new GetServerQuestsQuery(playerId), cancellationToken));
        }
    }
}
