using EmpireIdle.Application.Quests.Commands;
using EmpireIdle.Application.Quests.Queries;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace EmpireIdle.API.Controllers;

/// <summary>Квести гравця.</summary>
[ApiController]
[Authorize]
[Route("api/quests")]
public class QuestController : ControllerBase
{
    private readonly IMediator _mediator;

    public QuestController(IMediator mediator) => _mediator = mediator;

    /// <summary>Доступні квести з поточним прогресом.</summary>
    [HttpGet("{playerId:guid}")]
    [ProducesResponseType(typeof(List<QuestView>), StatusCodes.Status200OK)]
    public async Task<ActionResult<List<QuestView>>> GetQuests(Guid playerId, CancellationToken cancellationToken)
        => Ok(await _mediator.Send(new GetQuestsQuery(playerId), cancellationToken));

    /// <summary>Забрати нагороду за виконаний квест.</summary>
    [HttpPost("{playerId:guid}/{questKey}/claim")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> Claim(Guid playerId, string questKey, CancellationToken cancellationToken)
    {
        await _mediator.Send(new ClaimQuestRewardCommand(playerId, questKey), cancellationToken);
        return NoContent();
    }
}
