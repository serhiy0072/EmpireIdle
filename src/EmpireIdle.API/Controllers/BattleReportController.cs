using EmpireIdle.API.DTOs;
using EmpireIdle.Application.Battles.Commands;
using EmpireIdle.Application.Battles.Queries;
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
    [ProducesResponseType(typeof(List<BattleReportResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    public async Task<ActionResult<List<BattleReportResponse>>> GetReports(Guid playerId, [FromQuery] int take = 20, CancellationToken cancellationToken = default)
    {
        var reports = await _mediator.Send(new GetBattleReportsQuery(playerId, take), cancellationToken);

        var response = reports
            .Select(r => new BattleReportResponse(
                r.Id, r.MarchId, r.X, r.Y, r.TerrainType,
                r.TargetName, r.TargetLevel, r.Won,
                r.AttackerPower, r.DefenderPower, r.FoughtAt, r.IsRead,
                r.Lines
                    .Select(l => new BattleReportLineResponse(
                        l.UnitType, l.Sent, l.Survived, l.Wounded, l.Recoverable, l.Dead))
                    .ToList()))
            .ToList();

        return Ok(response);
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

