using EmpireIdle.API.DTOs;
using EmpireIdle.API.Swagger;
using EmpireIdle.Application.Marches.Commands;
using EmpireIdle.Application.Marches.Queries;
using EmpireIdle.Application.Scouting.Commands;
using EmpireIdle.Application.Scouting.Queries;
using EmpireIdle.Domain.Combat;
using EmpireIdle.Domain.ValueObjects;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace EmpireIdle.API.Controllers
{
    /// <summary>Походи армії.</summary>
    [ApiController]
    [Authorize]
    [Route("api/marches")]
    public class MarchController : ControllerBase
    {
        private readonly IMediator _mediator;

        public MarchController(IMediator mediator)
        {
            _mediator = mediator;
        }

        /// <summary>Активні походи гравця: у дорозі до цілі або додому, найближче прибуття — першим.</summary>
        [HttpGet("{playerId:guid}")]
        [ProducesResponseType(typeof(List<MarchResponse>), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
        public async Task<ActionResult<List<MarchResponse>>> GetMarches(Guid playerId, CancellationToken cancellationToken)
        {
            var marches = await _mediator.Send(new GetMarchesQuery(playerId), cancellationToken);

            var response = marches
                .Select(m => new MarchResponse(
                    m.Id, m.TargetType, m.TargetId, m.TargetName, m.TargetLevel, m.TargetX, m.TargetY, m.Intent, m.State,
                    m.HeroIds, m.DepartedAt, m.LegStartedAt, m.ArrivesAt,
                    m.Units.Select(u => new MarchUnitResponse(u.UnitType, u.Level, u.Count)).ToList()))
                .ToList();

            return Ok(response);
        }

        /// <summary>Відправити розвідників із вежі розвідки: без героя й юнітів, ціль бачить марш.</summary>
        [HttpPost("{playerId:guid}/scout")]
        [ProducesResponseType(typeof(Guid), StatusCodes.Status201Created)]
        [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
        [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
        public async Task<IActionResult> Scout(Guid playerId, [FromBody] SendScoutRequest request,
            CancellationToken cancellationToken)
        {
            var marchId = await _mediator.Send(new SendScoutCommand(playerId, request.TargetType, request.TargetId),
                cancellationToken);

            return Created((string?)null, marchId);
        }

        /// <summary>Останні звіти розвідки гравця, новіші першими.</summary>
        [HttpGet("{playerId:guid}/scout-reports")]
        [ProducesResponseType(typeof(List<ScoutReportResponse>), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
        public async Task<ActionResult<List<ScoutReportResponse>>> GetScoutReports(Guid playerId,
            CancellationToken cancellationToken)
        {
            var reports = await _mediator.Send(new GetScoutReportsQuery(playerId), cancellationToken);

            return Ok(reports
                .Select(r => new ScoutReportResponse(r.Id, r.TargetType, r.TargetId, r.TargetName, r.X, r.Y,
                    r.Outcome, r.DefencePower, r.Lootable, r.CreatedAt))
                .ToList());
        }

        /// <summary>
        /// Ворожі марші в дорозі на село гравця, села соклановців і споруди клану —
        /// найближче прибуття першим. Тривога приходить подією, це — стан після перезавантаження.
        /// </summary>
        [HttpGet("{playerId:guid}/incoming")]
        [ProducesResponseType(typeof(List<IncomingAttackResponse>), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
        public async Task<ActionResult<List<IncomingAttackResponse>>> GetIncoming(Guid playerId, CancellationToken cancellationToken)
        {
            var attacks = await _mediator.Send(new GetIncomingAttacksQuery(playerId), cancellationToken);

            return Ok(attacks
                .Select(a => new IncomingAttackResponse(
                    a.MarchId, a.Intent, a.TargetType, a.TargetId, a.TargetName, a.TargetOwnerId == playerId,
                    a.TargetX, a.TargetY, a.FromX, a.FromY, a.AttackerName, a.AttackerClanTag,
                    a.DepartedAt, a.ArrivesAt))
                .ToList());
        }

        /// <summary>Відправити армію до цілі: в атаку на монстра або підкріпленням до села союзника.</summary>
        [HttpPost("{playerId:guid}")]
        [ProducesResponseType(typeof(Guid), StatusCodes.Status201Created)]
        [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
        public async Task<IActionResult> SendMarch(Guid playerId, [FromBody] SendMarchRequest request, CancellationToken cancellationToken)
        {
            var units = request.Units.ToDictionary(kv => UnitStackKey.Parse(kv.Key), kv => kv.Value);

            var marchId = await _mediator.Send(
                new SendMarchCommand(playerId, request.TargetType, request.TargetId, units, request.HeroIds, request.Intent),
                cancellationToken);

            return Created((string?)null, marchId);
        }


        /// <summary>
        /// Прискорити марш за gems: зрізати обраний відсоток залишку дороги (50/75/90 — варіанти каталогу).
        /// Бій відбудеться найближчим проходом сканера.
        /// </summary>
        [HttpPost("{playerId:guid}/{marchId:guid}/speedup")]
        [ProducesResponseType(StatusCodes.Status204NoContent)]
        [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
        public async Task<IActionResult> SpeedUpMarch(Guid playerId, Guid marchId, [FromBody] SpeedUpMarchRequest request,
            CancellationToken cancellationToken)
        {
            await _mediator.Send(new SpeedUpMarchCommand(playerId, marchId, request.Percent), cancellationToken);
            return NoContent();
        }

        /// <summary>
        /// Відкликати табір (§2.5): армія, що не застала села на місці, йде додому маршем.
        /// </summary>
        [HttpPost("{playerId:guid}/{marchId:guid}/recall")]
        [ProducesResponseType(StatusCodes.Status204NoContent)]
        [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
        [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
        [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
        public async Task<IActionResult> RecallCamp(Guid playerId, Guid marchId, CancellationToken cancellationToken)
        {
            await _mediator.Send(new RecallCampCommand(playerId, marchId), cancellationToken);
            return NoContent();
        }

        /// <summary>
        /// Оцінка бою до відправки: смуга шансів, вердикт і шанс перемоги. Сили сторін — лише проти
        /// монстра; у чужого села їх показує розвідка, а не прев'ю.
        /// </summary>
        [HttpPost("{playerId:guid}/preview")]
        [WithoutIdempotencyKey]
        [ProducesResponseType(typeof(BattlePreviewResult), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
        [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
        public async Task<ActionResult<BattlePreviewResult>> PreviewBattle(Guid playerId,
            [FromBody] SendMarchRequest request, CancellationToken cancellationToken)
        {
            var units = request.Units.ToDictionary(kv => UnitStackKey.Parse(kv.Key), kv => kv.Value);

            var preview = await _mediator.Send(
                new GetBattlePreviewQuery(playerId, request.TargetType, request.TargetId, request.HeroIds, units),
                cancellationToken);

            return Ok(preview);
        }
    }
}
