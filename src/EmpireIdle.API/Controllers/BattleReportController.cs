using EmpireIdle.Application.Battles.Commands;
using EmpireIdle.Application.Battles.Queries;
using EmpireIdle.Application.Battles.ReadModels;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace EmpireIdle.API.Controllers;

/// <summary>Звіти про бої гравця.</summary>
[ApiController]
[Authorize]
[Route("api/battle-reports")]
public class BattleReportController : ControllerBase
{
    private readonly IMediator _mediator;

    public BattleReportController(IMediator mediator)
    {
        _mediator = mediator;
    }

    /// <summary>Останні звіти гравця (найновіші першими).</summary>
    [HttpGet("{playerId:guid}")]
    [ProducesResponseType(typeof(List<BattleReportView>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    public async Task<ActionResult<List<BattleReportView>>> GetReports(Guid playerId, [FromQuery] int take = 20, CancellationToken cancellationToken = default)
    {
        return Ok(await _mediator.Send(new GetBattleReportsQuery(playerId, take), cancellationToken));
    }

    /// <summary>Позначити звіт прочитаним.</summary>
    [HttpPost("{playerId:guid}/{reportId:guid}/read")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> MarkAsRead(Guid playerId, Guid reportId, CancellationToken cancellationToken)
    {
        await _mediator.Send(new MarkReportAsReadCommand(playerId, reportId), cancellationToken);
        return NoContent();
    }
}

